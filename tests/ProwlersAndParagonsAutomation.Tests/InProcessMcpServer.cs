using System.IO.Pipelines;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// A client and a server on either end of a pair of pipes, in this process. It is the real
/// protocol — initialize, the capability exchange, JSON-RPC framing — over streams that happen
/// not to be a console.
///
/// <para><b>One copy, for both servers.</b> <see cref="McpServerTests"/> and
/// <see cref="McpPlayServerTests"/> each used to carry their own version of this, and they were
/// the same code with a different name in it. That is the shape a fix lands in one of and not
/// the other, which is precisely what happened to the teardown order below.</para>
///
/// <para><b>The teardown order is the whole reason this file exists, and it is not a matter of
/// taste.</b> <c>ModelContextProtocol.Core</c> 2.2.0 — the pinned version, commit
/// <c>6fa3825</c> — has exactly one shutdown that <c>StreamServerTransport</c> treats as clean,
/// and it is <b>end of input</b>. Its read loop breaks on a null line and calls
/// <c>SetDisconnected(null)</c> (<c>Server/StreamServerTransport.cs</c> lines 107-111 and 181),
/// which completes the transport's message channel with no error, so
/// <c>McpSessionHandler.ProcessMessagesCoreAsync</c>'s <c>await foreach</c> ends normally and
/// <c>McpServerImpl.RunAsync</c> returns.</para>
///
/// <para><b>Disposing the transport instead is a race, and CI lost it.</b>
/// <c>StreamServerTransport.DisposeAsync</c> cancels its shutdown token and then, four lines
/// later, disposes the input reader — <c>Server/StreamServerTransport.cs</c> lines 277 and 282.
/// Disposing that reader completes the <see cref="PipeReader"/> underneath it, and a completed
/// reader is a landmine for the loop: <c>Pipe</c> raises
/// <c>ThrowInvalidOperationException_NoReadingAllowed</c> both from <c>ReadAsync</c>'s entry check
/// — which runs <em>before</em> the cancellation token is looked at — and from
/// <c>AdvanceReader</c>, which is where <c>PipeReaderStream.HandleReadResult</c> lands when a read
/// already in flight comes back. <b>The second is the one measured here</b>, by the mutation this
/// file's fix was proved with. So whichever of cancellation and completion lands first decides
/// whether the read loop comes out with an <see cref="OperationCanceledException"/> the transport
/// treats as clean or with
/// <c>InvalidOperationException: Reading is not allowed after reader was completed</c>, which it
/// hands to <c>SetDisconnected(error)</c>. A faulted channel faults
/// <c>ProcessMessagesCoreAsync</c> — it catches <see cref="OperationCanceledException"/> and
/// nothing else (<c>McpSessionHandler.cs</c> lines 218 and 352) — which faults
/// <c>RunAsync</c>, which is a red test in a run where nothing was wrong.
/// Run 34040527190 lost that race on a docs-only branch.</para>
///
/// <para><b>A narrow <c>catch</c> was the obvious fix and is the wrong one</b>, for the reason
/// <c>CLAUDE.md</c> gives about denylists: swallowing the one spelling leaves the race, and the
/// next thing it produces will have a different name. The order below removes it instead —
/// nothing completes the reader while the loop is still live.</para>
///
/// <para><b>The client cannot send that end-of-input itself.</b>
/// <c>StreamClientSessionTransport.CleanupAsync</c> (<c>Client/StreamClientSessionTransport.cs</c>
/// lines 188-211) cancels its own token and waits for its own read task; it never disposes the
/// streams it was handed. So the harness owns the pipe and completes the writer itself.</para>
/// </summary>
internal static class InProcessMcpServer
{
    /// <summary>
    /// How long the server's run is given to end on its own after end-of-input.
    ///
    /// <para>Generous on purpose: this is not a measurement of how fast a shutdown is, it is the
    /// bound that turns a leaked server task into a failing test rather than into a suite that
    /// never finishes. A correct shutdown takes milliseconds, so no loaded runner is anywhere
    /// near this.</para>
    /// </summary>
    internal static readonly TimeSpan EndsWithin = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Stands a server up over a pair of pipes, drives <paramref name="body"/> against a client
    /// connected to it, and then ends the run the one way the SDK ends cleanly.
    /// </summary>
    /// <param name="serverName">The server's name, for the transport's own diagnostics.</param>
    /// <param name="options">The options the server is built over — its tools and its identity.</param>
    /// <param name="body">What to drive over the client once it is connected.</param>
    internal static async Task Drive(
        string serverName, McpServerOptions options, Func<McpClient, Task> body)
    {
        var toServer = new Pipe();
        var toClient = new Pipe();

        var transport = new StreamServerTransport(
            toServer.Reader.AsStream(), toClient.Writer.AsStream(), serverName);

        var server = McpServer.Create(transport, options);

        var running = server.RunAsync();

        var client = await McpClient.CreateAsync(
            new StreamClientTransport(toServer.Writer.AsStream(), toClient.Reader.AsStream()));

        Exception? end;
        try
        {
            await body(client);
        }
        finally
        {
            end = await ShutDown(client, toServer.Writer, transport, server, running);
        }

        // Reached only when the body itself was happy. A complaint about the teardown must never
        // be what a failing test reports: this repository has a written history of a harness fault
        // being read as a product fault, and "the server did not shut down" on top of a real
        // assertion failure would be exactly that again.
        if (end is TimeoutException)
        {
            Assert.Fail(
                $"the {serverName} server was still running {EndsWithin.TotalSeconds:0}s after its "
                + "client's writer was completed, so this harness leaks a server task per test");
        }

        if (end is not null)
        {
            throw new InvalidOperationException(
                $"the {serverName} server's run faulted rather than ending on end-of-input", end);
        }

        // The positive control, and it is the one that matters: every assertion above is satisfied
        // by a run that never happened. `end is null` says nothing was thrown; this says the task
        // really did finish, and finished without faulting.
        Assert.True(
            running.IsCompletedSuccessfully,
            $"the {serverName} server's run did not complete successfully "
            + $"(status {running.Status})");
    }

    /// <summary>
    /// End of input, then the run, then the two things that own the streams — and returns what
    /// the run did rather than throwing it, so the caller can decide whether it is allowed to
    /// speak over the body's own failure.
    /// </summary>
    private static async Task<Exception?> ShutDown(
        McpClient client,
        PipeWriter toServer,
        StreamServerTransport transport,
        McpServer server,
        Task running)
    {
        // The client first, so nothing is in flight when the server is told there is no more
        // input. This does not close the pipe — see the class comment.
        await client.DisposeAsync();

        // End of input. The server's read loop reads a null line, breaks, and disconnects with no
        // error, which is the only clean end this SDK has.
        await toServer.CompleteAsync();

        Exception? failure = null;
        try
        {
            await running.WaitAsync(EndsWithin);
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        // Only now. The read loop has already left the reader, so completing it here cannot be
        // the thing the loop trips over.
        await transport.DisposeAsync();
        await server.DisposeAsync();

        return failure;
    }
}
