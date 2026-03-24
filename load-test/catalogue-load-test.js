/**
 * k6 load test — "Customer searches and views a product"
 *
 * Simulates the full catalogue flow:
 *   1. Customer lands on the search page and submits a query
 *   2. Customer clicks through to a product detail page
 *
 * Run:
 *   k6 run load-test/catalogue-load-test.js
 *
 * Run with more load:
 *   k6 run --vus 50 --duration 2m load-test/catalogue-load-test.js
 */

import http from "k6/http";
import { sleep, check } from "k6";
import { Rate } from "k6/metrics";

// Custom metric: track how often searches return a page with no products listed.
// This complements the server-side catalogue_search_zero_results_total counter
// and lets us correlate client-observed empty results with server metrics.
const emptySearchRate = new Rate("empty_search_results");

export const options = {
  stages: [
    { duration: "30s", target: 10 },  // ramp up to 10 virtual users
    { duration: "2m",  target: 10 },  // hold — generates steady signal in Grafana
    { duration: "30s", target: 0 },   // ramp down
  ],
  thresholds: {
    // p95 of the full flow should stay under 2 s
    http_req_duration: ["p(95)<2000"],
    // fewer than 5 % of requests should fail
    http_req_failed: ["rate<0.05"],
  },
};

// Search terms that cover the catalogue: some will return results, some won't.
// The mix of hits and misses exercises both code paths and produces signal
// on the zero-results metric in Grafana.
const SEARCH_TERMS = [
  "apple",
  "laptop",
  "smartphone",
  "camera",
  "headphones",   // likely zero results — exercises the zero-results counter
  "notebook",
  "book",
  "phone",
  "zzznoresults", // guaranteed zero results
  "computer",
];

// Product slugs from the sample catalogue — covers different categories
// so the product_page.duration histogram shows per-category breakdowns.
const PRODUCT_PAGES = [
  "/apple-macbook-pro",
  "/htc-smartphone",
  "/build-your-own-computer",
  "/apple-iphone-16-128gb",
  "/apple-icam",
];

function randomItem(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

export default function () {
  const BASE = "http://localhost";

  // Step 1: search
  const term = randomItem(SEARCH_TERMS);
  const searchRes = http.get(`${BASE}/search?q=${term}`, {
    tags: { flow_step: "search" },
  });

  check(searchRes, {
    "search: status 200": (r) => r.status === 200,
  });

  // Detect empty results by looking for the "no products" indicator in the HTML.
  // nopCommerce renders a specific message when no products match.
  const isEmpty =
    searchRes.body &&
    (searchRes.body.includes("No products were found") ||
      searchRes.body.includes("no-result"));
  emptySearchRate.add(isEmpty ? 1 : 0);

  // A real user won't click a product if the search returned nothing.
  // Skip step 2 to make the throughput graphs diverge realistically.
  if (isEmpty) {
    sleep(Math.random() * 2 + 1); // user gives up and moves on
    return;
  }

  sleep(Math.random() * 1.5 + 0.5); // 0.5–2 s think time between search and click

  // Step 2: view a product page
  const productSlug = randomItem(PRODUCT_PAGES);
  const productRes = http.get(`${BASE}${productSlug}`, {
    tags: { flow_step: "product_view" },
  });

  check(productRes, {
    "product: status 200": (r) => r.status === 200,
  });

  sleep(Math.random() * 2 + 1); // 1–3 s reading time on the product page
}
