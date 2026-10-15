# Polite Scraper — FlyRank BE-05 / A9

A polite scraping pipeline for [Books to Scrape](https://books.toscrape.com/): it downloads the
first three catalogue pages, visits every book detail page, turns messy HTML into clean
schema-checked JSON, survives a broken page, and writes an honest run report.

## Target classification

- **Site:** Books to Scrape — `https://books.toscrape.com/`
- **Why:** it is a sandbox built for practising scraping, so crawling it is exactly what it is for.
- **How much:** the first 3 catalogue pages only (20 books each, 60 books total).
- **What data:** title, product_url, price_text, availability_text, rating_text, description,
  source_page, fetched_at, and a numeric price_gbp.
- **Why that is appropriate here:** the site is explicitly a scraping practice sandbox, so a small,
  rate-limited, read-only crawl stays inside its purpose.
- **robots.txt:** `404 — no robots file found` (a missing file is not permission).

I will not reuse this code on another site without checking its rules and terms first.
