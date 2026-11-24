# Demo video script (about 2 minutes)

Record at 1080p. Use a throwaway subscription key, or rotate the key and secret after recording.
Do not show the subscription key, the shared secret or your subscription ID on screen.

| Time | On screen | Say |
|---|---|---|
| 0:00 | README top and the architecture diagram | "This is a claims triage assistant. Three AI agents read a claim and check it against the policy, and plain code applies safety rules before anything is saved." |
| 0:15 | Terminal: call the API through the gateway with a key (CLM-2001) | "A client calls the API through API Management with a key. This is a wind claim." |
| 0:30 | The JSON response | "The result has a decision, a summary, the cited policy clauses and a review flag." |
| 0:45 | Call CLM-2002 (flood) | "A flood claim. The policy excludes flood, so it is denied, and denials always go to a person." |
| 1:00 | The guardrailFlags in the response | "That flag comes from code, not from the model." |
| 1:10 | Terminal: the same call without a key | "Without a key, the gateway refuses the call." |
| 1:20 | Azure portal: Application Insights, the trace of the CLM-2002 request | "Here is that request as a trace: each agent step, the model calls, and the tool calls on the MCP server." |
| 1:45 | GitHub Actions run, then the repository | "Every push to main is tested and deployed by this pipeline, with no stored cloud password." |
| 1:55 | README "What I would do differently" | "The code is on GitHub, with my notes on what I would improve next. Thanks for watching." |

Tips

- Rehearse once, then record in one take. Trim the start and end only.
- Zoom the terminal font so it is readable on a phone.
- Run each call once before recording, so the API is warm (it scales to zero when idle).
- Keep it under two minutes. A short video gets watched.
