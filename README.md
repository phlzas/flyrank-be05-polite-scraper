# Polite Scraper — FlyRank BE-05 / A9

A polite scraping pipeline for [Books to Scrape](https://books.toscrape.com/): it downloads the
first three catalogue pages, visits every book detail page, turns messy HTML into clean
schema-checked JSON, survives a broken page, and writes an honest run report.

## Lane

- **Lane:** **.NET 9 (C#)** — not the JavaScript or Python lane the brief lists.
- **Why this lane:** the brief says *"Not sure? Stay with the language you used earlier in the
  track."* My backend-track work has been in C# / .NET, so staying in .NET keeps the whole track on
  one toolchain and one mental model instead of splitting it across two languages. Every tool the
  brief names maps 1:1 onto the .NET equivalent, so nothing about the assignment is lost:

  | Brief (JS / Python) | This repo (.NET) |
  |---------------------|------------------|
  | `fetch` / `requests` | `HttpClient` |
  | Cheerio / Beautiful Soup | AngleSharp |
  | Zod / Pydantic | JsonSchema.Net (JSON Schema draft 2020-12) |
  | `JSON` / `json` module | `System.Text.Json` |
- **How to install it:** install the [.NET SDK 9.0](https://dotnet.microsoft.com/download) (or newer)
  for your platform. Then check it:

  ```powershell
  dotnet --version   # should print 9.x
  ```

  That single SDK is the only prerequisite — no package manager step, no virtual environment.

## Target classification

- **Site:** Books to Scrape — `https://books.toscrape.com/`
- **Why:** it is a sandbox built for practising scraping, so crawling it is exactly what it is for.
- **How much:** the first 3 catalogue pages only (20 books each, 60 books total) — one documented
  command, one bounded crawl.
- **What data:** title, product_url, price_text, availability_text, rating_text, description,
  source_page, fetched_at, and a numeric price_gbp.
- **Why that is appropriate here:** the site explicitly exists as a scraping practice sandbox, so a
  small, rate-limited, read-only crawl stays inside its purpose.
- **robots.txt:** `404 — no robots file found` (a missing file is not permission, it is just a
  missing file).

I will not reuse this code on another site without checking its rules and terms first.

## Run it (under 5 minutes)

```powershell
git clone https://github.com/phlzas/flyrank-be05-polite-scraper.git
cd flyrank-be05-polite-scraper
dotnet run --project src/PoliteScraper --configuration Release
```

The first run fetches the three catalogue pages and all 60 book pages politely (at least 500 ms
between real requests), so it takes roughly half a minute. Every page is cached under `cache/`, so a
second run is instant and makes no network requests for pages it already has.

### Output

| File | What it is |
|------|------------|
| `output/books.json` | 60 clean, schema-validated book records. |
| `output/run-report.json` | Honest run summary: pages fetched, cache hits, valid/invalid records, failed pages. |
| `errors.json` | Only written when something fails; one entry per failed page with a reason. |

## Record schema

The contract every stored record must satisfy lives in
[`schema/book.schema.json`](schema/book.schema.json) (JSON Schema, draft 2020-12). A record is
written to `books.json` **only** if it validates; a record that fails goes to `errors.json` with the
reason instead. The shape:

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `title` | string | yes | `h1` inside the product area. |
| `product_url` | string | yes | absolute `https://` URL — the record's canonical identity. |
| `price_text` | string | yes | raw price exactly as shown, e.g. `"£51.77"`. |
| `availability_text` | string | yes | whitespace-collapsed, e.g. `"In stock (22 available)"`. |
| `rating_text` | string | yes | word form, e.g. `"Three"`. |
| `description` | string \| null | yes (key present) | **nullable** — `null` when the page has no description, never invented. |
| `source_page` | string | yes | absolute `https://` URL of the catalogue page it was found on (provenance). |
| `fetched_at` | string | yes | ISO-8601 UTC, `yyyy-MM-ddTHH:mm:ssZ` (provenance). |
| `price_gbp` | number | yes | `price_text` parsed into a real number, e.g. `51.77`. |

A real record looks like this:

```json
{
  "title": "A Light in the Attic",
  "product_url": "https://books.toscrape.com/catalogue/a-light-in-the-attic_1000/index.html",
  "price_text": "£51.77",
  "availability_text": "In stock (22 available)",
  "rating_text": "Three",
  "description": "It's hard to imagine a world without A Light in the Attic. …",
  "source_page": "https://books.toscrape.com/catalogue/page-1.html",
  "fetched_at": "2026-10-15T17:42:29Z",
  "price_gbp": 51.77
}
```

## How it stays polite

- A descriptive `User-Agent` on every request:
  `FlyRankInternship-A9/1.0 (+https://github.com/phlzas/flyrank-be05-polite-scraper)`.
- At least **500 ms between real requests**; cached reads never wait.
- A **10 s timeout**, and a **single retry** only on timeouts or 5xx responses — never on 404/403.
- Only HTTP **200** responses are treated as pages: anything else is never parsed and never cached.
- `robots.txt` is checked on every run and never cached, because a missing file is not permission.
- Development reads from the cache, so the site feels exactly one crawl.

## One honest limitation

The politeness wait is a **fixed 500 ms** with no awareness of the server's wishes: the scraper does
not read `Retry-After`, does not back off exponentially, and keeps no rate-limit state between runs
— so on a slower or busier server than this sandbox, one retry after a fixed delay is the whole
safety net. It also trusts the catalogue's own `li.next` chain as the only way pages link together:
if the site later changes that markup, discovery stops early instead of erroring loudly. The run
report is how you would notice — `pages_fetched` and `valid_records` would drop below the expected
60.

## Run report (real output)

This is the actual `output/run-report.json` from a clean first run (empty `cache/`), produced by the
documented command:

```json
{
  "start_time": "2026-10-15T17:42:28Z",
  "duration": 33.05,
  "pages_fetched": 64,
  "cache_hits": 0,
  "valid_records": 60,
  "invalid_records": 0,
  "failed_pages": 0
}
```

64 real fetches = 1 robots check + 3 catalogue pages + 60 book pages, one slow polite pass. Run it
again and the same file reports roughly `pages_fetched: 1, cache_hits: 63` in about two seconds —
the work is identical, only the source changed. Add one deliberately broken URL
(`--stage 5 --include-broken-url`) and the run still finishes with `valid_records: 60` and
`failed_pages: 1`, the bad page recorded in `errors.json`.

**Why this needed no browser:** the data is already in the HTML the server sends, so a browser would
only add cost — it would spin up a whole rendering engine to produce exactly the bytes a single HTTP
request already returns.

## Ethics

Use an official API when one exists — if a site offers data through a documented, sanctioned
endpoint, that is the front door, not its HTML. Never bypass logins, paywalls, or blocks: if a site
says no, that is the answer, not an obstacle. And collect only what you need — this scraper takes a
bounded slice (3 pages, 60 books, no personal data, no reuse beyond the sandbox), so a stranger
running it can trust that it stays a polite guest.

## Stage checkpoints

Each stage is a separate commit and can be run on its own:

```powershell
dotnet run --project src/PoliteScraper --configuration Release -- --stage 0   # classify the target
dotnet run --project src/PoliteScraper --configuration Release -- --stage 1   # fetch + cache page 1
dotnet run --project src/PoliteScraper --configuration Release -- --stage 2   # discover 3 pages / 60 urls
dotnet run --project src/PoliteScraper --configuration Release -- --stage 3   # extract raw records
dotnet run --project src/PoliteScraper --configuration Release -- --stage 4   # normalize + validate + write
dotnet run --project src/PoliteScraper --configuration Release -- --stage 5   # survive failures + report
```

To prove the pipeline survives a broken page without touching the real site's data, inject a fake
URL (it 404s, is never cached, and is recorded in `errors.json`):

```powershell
dotnet run --project src/PoliteScraper --configuration Release -- --stage 5 --include-broken-url
```

## Project layout

```
src/PoliteScraper/
  Program.cs     stage orchestration + CLI
  Config.cs      URLs, politeness limits, resolved paths
  Fetcher.cs     HTTP + cache + politeness + retry
  Catalog.cs     catalogue discovery (pages + book urls)
  Extractor.cs   HTML -> raw record (8 keys)
  Normalizer.cs  raw record -> clean record (+ price_gbp)
  Validation.cs  JSON Schema validation
  Report.cs      run report + errors.json
schema/book.schema.json   the contract every record must satisfy
```
