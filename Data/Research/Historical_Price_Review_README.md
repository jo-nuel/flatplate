# Historical price review data

FreshCrawl_Historical_Price_Review.csv contains 40 proposed pairs from the pinned 17 September 2026 snapshot. It preserves the original B017B evidence and pending human flags; it is not approved production data or a Price CSV V1 file.

Source: https://huggingface.co/datasets/FreshCrawl/australian-supermarket-prices/tree/4f802169940df31e09f2612f6a6288ffd72dc4fd

The user selected all 40 for review and accepted Coles Crisco Vegetable Oil SKU126745 as a historical reference (4.80 AUD /750mL). Its historical stock=false remains in the evidence. The original table's Pending oil-choice cell is retained as the initial audit state; the later inclusion policy does not imply live availability or personal verification.

Use the existing manual Recipe Library import of the two files in Data/Seeds before any future approved price action. Keep these folders with the full project ZIP; do not rely on developer-specific absolute paths. No application startup or importer automatically reads this research file. HistoricalPriceRecord/HistoricalPriceCsvReader are only the first read-only code review step; no complete save/UI workflow is delivered.

Student product review and code review remain pending. No production import file or automatic approval is generated.
