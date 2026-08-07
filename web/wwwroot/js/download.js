// Hands a string to the browser as a file download. This is the whole of the JavaScript
// in the app: everything else — the rules, the costing, the validation — is the C# engine
// running in WebAssembly.
window.ppDownload = (fileName, mimeType, contents) => {
    const blob = new Blob([contents], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
};

// The theme is a data-mode attribute on <html>, so every CSS custom property switches at
// once and no component ever needs to know which palette is active.
window.ppSetMode = (mode) => {
    document.documentElement.setAttribute("data-mode", mode);
};
