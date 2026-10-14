# Historical reference prices: manual import

1. In Recipe Library, manually import the two prepared recipe CSVs from `Data/Seeds` first. Existing recipes and user edits are preserved.
2. In Price Book choose **Import Historical Prices**.
3. Select `FreshCrawl_Historical_Prices_Preparation.csv` (40 rows, eight columns).
4. Select `FreshCrawl_Historical_Price_Review.csv` as the original evidence.
5. Review the preview. Tick the explicit whole-batch confirmation, then choose **Confirm Import**. Cancel/close before confirmation saves nothing. No personal name or individual approval receipt is required.

The original evidence hash pins dataset revision `4f802169940df31e09f2612f6a6288ffd72dc4fd`. Its archived Pending flags are preserved as original research, not the current confirmation state. Every price, SKU, pack size, unit, date and stock value must match this evidence. Both selected files are checked again before saving. The transaction reloads and checks the source, all forty records, database ingredient/store identities and existing price pairs. An existing pair blocks the whole batch; nothing is overwritten or deleted. Duplicate imports are rejected.

All new prices refer to 17 September 2026, not current prices. SQLite stores the date using an unspecified-kind midnight placeholder, without inventing an observation time or timezone. Coles Vegetable Oil SKU 126745 retains AUD 4.80 / 750 mL and stock=false in the evidence. It is a historical reference, not a current availability claim. Source is Seeded, as supported by the existing enum.

Keep `Data/Research` and `Data/Seeds` in the full project ZIP. Files are selected by the user, with no developer-specific paths. Ordinary seven-column Price CSV V1 is unchanged; historical date-only records use this separate importer. Historical exports have no timezone and cannot be reimported through ordinary V1 without a real, explicitly supplied timestamp policy.

## Student review and AI disclosure

AI assisted Person B implementation, validation and documentation. The student must review the final diff, understand the transaction and validation flow, and personally run the application before final submission. Automated isolated tests are not personal product verification or manual acceptance.
