// The one file Cloudflare Pages routes, and it does nothing.
//
// A catch-all under `/api/` so that everything real lives in `worker/`, where it is ordinary
// JavaScript that a test can import and run under Node. A directory of routed files would put
// the logic in files whose reachability is decided by their path, which is both harder to test
// and easier to expose by accident.
//
// Everything under `/api/` reaches this; every other address is a static asset and never
// touches the server.

import { handle } from '../../worker/index.js';

export const onRequest = ({ request, env }) => handle(request, env);
