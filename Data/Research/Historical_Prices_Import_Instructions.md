# Historical prices: manual import

These files are review material. No product has been approved automatically. Complete your personal product review before supplying a real approval receipt.

1. Use Recipe Library to import Person B's eight prepared recipes first, if they are not already present.
2. In Price Book, select **Import Historical Prices**.
3. Select `FreshCrawl_Historical_Prices_Preparation.csv` (40 rows, eight columns).
4. Select `FreshCrawl_Historical_Price_Review.csv` as the original 70-column evidence file. Do not change its original Pending flags.
5. Select your separately completed approval receipt JSON. The application does not create this receipt or fill in review decisions.
6. Review the preview, confirm the acknowledgement only after your personal review, and press **Confirm Import**. Cancel or close at any point before confirmation to save nothing.

The preparation CSV is a deterministic projection of the original evidence: IngredientName, StoreName, PriceAUD, PackSizeBase, BaseUnit, ObservedDate, ProductId, InStockAtObservation. It is not ordinary Price CSV V1 and contains no approval decisions.

The receipt uses the existing `HistoricalPriceApprovalReceipt` structure, with exact case-sensitive property names:

- `ReviewerName`: your actual name; `ReviewedOn`: the actual review date, `yyyy-MM-dd`.
- `EvidenceSha256`: `aaa279633e551e3b45e1f61d88702d6298504c8da3d4f056c1d0a7fad9bb2995`.
- `DatasetRevision`: `4f802169940df31e09f2612f6a6288ffd72dc4fd`.
- `Items`: exactly 40 unique ingredient/store decisions. Each item contains `Record`, `PersonallyReviewed`, and `ApprovedForImport`.
- Each `Record` contains the eight CSV fields with matching values: names/units/product ID/date as strings, price and quantity as exact decimal JSON numbers, stock as a JSON boolean.
- Both decision booleans must reflect your actual completed review and approval. Missing, false or inconsistent decisions block the batch. The acknowledgement checkbox does not approve missing receipt decisions.

The observed date remains 17 September 2026 without an invented timezone. Coles Vegetable Oil SKU 126745 retains AUD 4.80 / 750 mL and stock=false in the source evidence. It is a historical reference, not a claim of current availability.

Confirmation rechecks all selected file hashes and calls the transactional historical importer. The importer revalidates original evidence, the complete receipt, ingredient/store identities and existing price pairs inside its transaction. Any existing pair blocks the whole batch; existing prices are never overwritten. A repeated import is rejected.

Keep this Data/Research directory and Data/Seeds with the complete project ZIP. File selection uses paths chosen by the user and does not depend on a developer-specific absolute path. No approved receipt is included with the project.
