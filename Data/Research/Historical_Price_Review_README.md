# Historical price evidence

`FreshCrawl_Historical_Price_Review.csv` preserves forty original observations from the pinned 17 September 2026 snapshot, including original research flags. Those flags do not represent the current import confirmation.

Source: https://huggingface.co/datasets/FreshCrawl/australian-supermarket-prices/tree/4f802169940df31e09f2612f6a6288ffd72dc4fd

`FreshCrawl_Historical_Prices_Preparation.csv` is the deterministic eight-column projection for the separate historical importer. Neither file is ordinary Price CSV V1. Coles Vegetable Oil SKU126745 is a historical reference (AUD4.80/750mL, stock=false); original evidence remains unchanged.

See [manual import instructions](Historical_Prices_Import_Instructions.md). Nothing is imported automatically at startup. Import the prepared recipes first, then review and explicitly confirm the historical batch in Price Book. No personal-name or individual approval receipt is required. Keep both research files and the recipe seed CSVs with the full project.
