# Arkik Opportunity Classifier — Demo Guide

## 1. The one-sentence pitch

> Paste a Jira ticket URL, and AI tells you which Arkik opportunity it belongs to —
> and highlights the exact sentence that proves it.

## 2. Arkik opportunity addressed

**Primary:** Backlog prioritization
**Secondary:** Clearer requirements and definitions · Understanding operational needs

## 3. Intended user

| User | Why they care |
| --- | --- |
| Product Owners / BAs | Triage and tag the incoming backlog consistently |
| Delivery leads | See how effort is distributed across opportunity areas |
| Leadership | Evidence-based view of where the backlog actually invests |

## 4. Benefits

### Time saved
- Reading a long Jira ticket and deciding its category takes roughly **2–3 minutes**
  of focused attention. The tool returns an answer in **~3 seconds**.
- The reviewer no longer has to read the whole description: the justifying sentence
  is highlighted, so they jump straight to the relevant part.
- **Measure it during the demo:** time yourself reading ARKIK-42798 manually versus
  clicking Analyze. That is your live, credible benefit number.

### Consistency
- Twelve people tagging tickets by hand produce twelve different interpretations.
  One model with one prompt applies the **same criteria to every ticket**.
- Re-running the tool on the same ticket gives the same answer (temperature = 0),
  so the classification is reproducible and auditable.

### Transparency / trust
- Every answer carries a **confidence score** and a **written reason** — it is never
  a black box.
- The highlighted quote lets a human verify the decision in seconds instead of
  re-reading the ticket.
- A visible badge shows whether the answer came from **AI** or the offline fallback.

### Challenge and explore
- Click any of the 12 opportunities and the AI re-argues the case for that choice.
- Low confidence on an alternative (e.g. 20%) is itself a signal that the original
  classification was sound.

### Low cost / low risk
- Runs on a **free** AI tier; no data leaves as training material.
- Provider-agnostic: any OpenAI-compatible endpoint works via configuration only.
- If AI is unavailable, the keyword fallback keeps the app functional.

### Scale argument
- Benefit scales linearly with backlog size. Ticket key ARKIK-42798 implies a very
  large ticket history — apply your per-ticket saving to your real monthly volume.
- Formula for the slide: `tickets/month × minutes saved ÷ 60 = hours returned/month`

## 5. Live demo script (5 minutes)

1. **(30s) Frame the problem.** "Our backlog has thousands of tickets. Knowing which
   strategic opportunity each one serves is manual, slow, and inconsistent."
2. **(30s) Show the app.** Point out the connected Jira account in the header — this
   is reading live production Jira, not mock data.
3. **(60s) Paste ARKIK-42798 and click Analyze.** Talk while it runs: fetches the
   ticket from the Jira REST API, flattens the Atlassian document format, sends it
   to the model.
4. **(60s) Read the result.** Opportunity, the green **AI** badge, confidence, and
   the AI-written reason. Scroll to the yellow highlight in the description.
5. **(60s) Challenge it.** Click "Growing technical debt". The AI re-argues, and
   confidence drops — demonstrating genuine reasoning rather than keyword matching.
6. **(30s) Close with the benefit.** Per-ticket time saving × backlog volume, plus
   consistency and auditability.

## 6. Likely questions

| Question | Answer |
| --- | --- |
| Is this really AI? | Yes — Groq `openai/gpt-oss-120b`. The badge shows AI vs fallback, and the reason text is written per ticket. |
| What if the AI is wrong? | It is advisory. Confidence + quote let a human check in seconds, and any opportunity can be re-argued. |
| What if the AI is down? | Keyword fallback keeps it running; the badge turns amber so nobody is misled. |
| Data security? | Only ticket summary and description are sent. Could move to a Cemex-hosted model. |
| Does it log in as me? | Currently one service account. Per-user Atlassian OAuth is the next step. |
| Cost? | Free tier today. |

## 7. Technical talking points

- .NET 10 · ASP.NET Core · Blazor Server
- REST API (`/api/JiraOpportunity/classify`, `/explain`, `/classify-text`) + Swagger,
  so it can be automated or embedded in another tool
- Live Jira Cloud REST API v3 integration with Atlassian Document Format parsing
- **35 unit tests**, all passing — covers AI path, fallback, error handling
- Secrets in user-secrets, never committed

## 8. Next steps if adopted

1. **Bulk mode** — classify a whole JQL query and produce a distribution chart
   ("38% of our backlog is technical debt") — the real leadership value.
2. **Write back to Jira** — set a label or custom field automatically.
3. **Per-user Atlassian OAuth** instead of a shared service token.
4. **Tune on real labelled data** to raise accuracy.

