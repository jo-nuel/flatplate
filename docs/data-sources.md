# Price data source

FlatPlate's starter prices are a curated extract from the `products`
configuration of
[FreshCrawl/australian-supermarket-prices](https://huggingface.co/datasets/FreshCrawl/australian-supermarket-prices).
The source is licensed for research and educational use. Every source row was
collected on 17 September 2026.

The complete source contains 48,867 online products: 23,767 from Coles and
25,100 from Woolworths. FlatPlate stores only the 70 rows needed to price its
35 starter ingredients at both supermarkets. The committed CSV preserves each
retailer's product ID and product name so a selection can be traced back to the
source snapshot.

Pack sizes are converted to FlatPlate's base units: grams for weight items,
millilitres for volume items, and pieces for count items. Any count or weight
that is not stated directly by the retailer is labelled in the CSV's
`normalisation_note` column. Special prices are retained only when the dataset
did not provide a suitable regular-priced equivalent, and those rows are also
labelled.

## Limitations

- The snapshot represents one default online store per chain and is not a
  measurement of local shelf prices.
- It covers one day and will not reflect later price or availability changes.
- Online ranges can differ from products offered in physical supermarkets.
- Normalised estimates for produce packs are suitable for planning but are not
  exact measurements.

If authorised supermarket APIs were available, FlatPlate would keep the same
`IPriceProvider` boundary and add an API-backed provider. It would authenticate
with credentials stored outside source control, request current products for a
selected store, map the response into `PriceEntry` records, retain the API's
timestamp and product identifier, cache successful responses in SQLite, and
fall back to the seeded snapshot when the API was unavailable.
