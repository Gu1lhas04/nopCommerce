﻿# Assignment 01 — Observability in nopCommerce

This fork adds OpenTelemetry distributed tracing and custom metrics to nopCommerce as
part of Individual Assignment 01. The instrumented flow is **"Customer searches and
views a product"**, covering the Catalogue, Search, and Pricing services.

---

## Architecture of the instrumented flow

```mermaid
flowchart TD
    k6["k6 / Browser"]

    subgraph nop[nopCommerce]
        mw["ASP.NET Core\nMiddleware"]
        ctrl["CatalogController\nProductController"]
        svc["ProductService"]
        iep["InstrumentedEventPublisher"]
        db[("SQL Server")]

        mw --> ctrl --> svc --> db
        svc --> iep
    end

    subgraph obs[Observability Backend]
        prom["Prometheus\n:9090"]
        jaeger["Jaeger\n:16686"]
        grafana["Grafana\n:3000"]
    end

    k6 -->|HTTP| mw
    mw -.->|spans| jaeger
    iep -.->|spans| jaeger
    ctrl -->|"metrics"| prom
    prom --> grafana
    jaeger --> grafana
```

**Custom instrumentation points:**

| Signal | Name | What it measures |
|--------|------|-----------------|
| Span | `catalogue.search` | Full duration of a search request; tags: `search.query_length`, `search.category_id`, `search.result_count` |
| Span | `catalogue.product_page` | Product detail page load; tags: `product.id`, `product.category` |
| Span | `event.<EventName>` | Every internal domain event dispatched via `IEventPublisher`, linked to the originating request trace |
| Histogram | `catalogue.product_page.duration` | Time (ms) to prepare a product detail page, by category |
| Counter | `catalogue.search.zero_results` | Searches that returned no products, by category |

Sensitive data (raw search keywords, product names, customer identifiers) is
deliberately excluded from all spans and metric labels.

---

## How to build and run

### Prerequisites

- Docker and Docker Compose
- k6 (for the load test): `sudo snap install k6`

### Start the full stack

```bash
docker compose up --build
```

This starts five containers:

| Container | Port | Purpose |
|-----------|------|---------|
| `nopcommerce` | 80 | The nopCommerce web application |
| `nopcommerce_mssql_server` | — | SQL Server database |
| `jaeger` | 16686 | Trace storage and UI (OTLP gRPC :4317) |
| `prometheus` | 9090 | Metrics scraping and storage |
| `grafana` | 3000 | Dashboard visualisation |

On first run, nopCommerce will run its installation wizard. Complete it using the
pre-configured SQL Server connection:

- **Server:** `nopcommerce_mssql_server`
- **Database:** `nopcommerce`
- **User:** `sa`
- **Password:** `nopCommerce_db_password`

---

## How to view the Grafana dashboard

1. Open **http://localhost:3000** (credentials: `admin` / `admin`)
2. Navigate to **Dashboards → Catalogue Search & Product View**
3. The dashboard is provisioned automatically from
   `observability/grafana/provisioning/dashboards/catalogue-dashboard.json`

The dashboard is organised into three sections:

| Section | What it answers |
|---------|----------------|
| **1 — Is it alive?** | Searches/min, product page views/min, error rate, p95 latency |
| **2 — Is it fast?** | Product page load duration (p50/p95/p99) by category |
| **3 — Are there silent failures?** | Zero-result search rate vs total searches; 5xx error rate |

---

## How to run the load test

The load test uses [k6](https://k6.io) and drives the full search → product view flow.

```bash
# Default: 10 virtual users, ~3 minutes total
k6 run load-test/catalogue-load-test.js

# Higher load (more signal in Grafana)
k6 run --vus 50 --duration 2m load-test/catalogue-load-test.js
```

The script ramps up to the target VU count over 30 seconds, holds for 2 minutes, then
ramps down. Each virtual user:
1. Picks a random search term and hits `/search?q=<term>`
2. Waits 0.5–2 seconds (think time)
3. Picks a random product page and loads it
4. Waits 1–3 seconds (reading time)

**Thresholds:** p95 response time < 2 s, error rate < 5 %.

Open the Grafana dashboard before starting the test to watch metrics respond in real time.

---

nopCommerce: free and open-source eCommerce solution
===========

[nopCommerce](https://www.nopcommerce.com/?utm_source=github&utm_medium=content&utm_campaign=homepage) is the best open-source eCommerce platform. nopCommerce is free, and it is the most popular ASP.NET Core shopping cart.

![nopCommerce demo](https://www.nopcommerce.com/images/github/responsive_devices_codeplex.png#v1)

### Key features ###

* The product is being developed and supported by the professional team since 2008.
* nopCommerce has been downloaded more than 3,000,000 times.
* The active developer community has more than 250,000 members.
* nopCommerce runs on .NET 9 with an MS SQL 2012 (or higher) backend database.
* nopCommerce is cross-platform, and you can run it on Windows, Linux, or Mac.
* nopCommerce supports Docker out of the box, so you can easily run nopCommerce on a Linux machine.
* nopCommerce supports PostgreSQL and MySQL databases.
* nopCommerce fully supports web farms. You can read more about it [here](https://docs.nopcommerce.com/en/developer/tutorials/web-farms.html?utm_source=github&utm_medium=referral&utm_campaign=documentation&utm_content=text).  
* All methods in nopCommerce are async.
* nopCommerce supports multi-factor authentication out of the box.
* Start our [online course for developers](https://nopcommerce.com/training?utm_source=github&utm_medium=referral&utm_campaign=course&utm_content=text) and get the practical and technical skills you need to run and customize nopCommerce websites.

![Logo](https://www.nopcommerce.com/images/github/logos.png#v2)

nopCommerce architecture follows well-known software patterns and the best security practices. The source code is fully customizable. Pluggable and clear architecture makes it easy to develop custom functionality and follow any business requirements.

Using the latest Microsoft technologies, nopCommerce provides high performance, stability, and security. nopCommerce is also fully compatible with Azure and web farms.

Our clear and detailed [documentation](https://docs.nopcommerce.com/developer/index.html?utm_source=github&utm_medium=referral&utm_campaign=documentation&utm_content=text) and [online course](https://nopcommerce.com/training?utm_source=github&utm_medium=referral&utm_campaign=course&utm_content=text) for developers will help you start with nopCommerce easily.


### The advantages of working with nopCommerce ###

nopCommerce offers powerful [out-of-the-box features](https://www.nopcommerce.com/features?utm_source=github&utm_medium=referral&utm_campaign=features&utm_content=text) for creating an online store of any size and type.

nopCommerce is integrated with all the popular third-party services. You can find thousands of integrations on nopCommerce [Marketplace](https://www.nopcommerce.com/marketplace?utm_source=github&utm_medium=referral&utm_campaign=marketplace&utm_content=text).

The [Web API plugin](https://www.nopcommerce.com/web-api?utm_source=github&utm_medium=referral&utm_campaign=WebAPI&utm_content=text) by the nopCommerce team lets you build integrations with third-party services or mobile applications using REST. The Web API plugin is available with source code and covers all methods of nopCommerce: backend and frontend. You can read more about it [here](https://www.nopcommerce.com/web-api?utm_source=github&utm_medium=referral&utm_campaign=WebAPI&utm_content=text).

Friendly members of the [nopCommerce community](https://www.nopcommerce.com/boards?utm_source=github&utm_medium=referral&utm_campaign=forum&utm_content=text) will always help with advice and share their experiences. nopCommerce core development team provides [professional support](https://www.nopcommerce.com/nopcommerce-premium-support-services?utm_source=github&utm_medium=referral&utm_campaign=premium_support&utm_content=text) within 24 hours.


## Store demo ##

Evaluate the functionality and convenience of nopCommerce as a customer and store owner.

Front End | Admin area
----|------
[![ScreenShot](https://www.nopcommerce.com/images/github/public-demo.png#v1)](https://demo.nopcommerce.com?utm_source=github&utm_medium=referral&utm_campaign=demo_store&utm_content=button) | [![ScreenShot](https://www.nopcommerce.com/images/github/admin-demo.png#v1)](https://admin-demo.nopcommerce.com/admin?utm_source=github&utm_medium=referral&utm_campaign=demo_store&utm_content=button)


### nopCommerce resources ###

nopCommerce official site: [https://www.nopcommerce.com](https://www.nopcommerce.com/?utm_source=github&utm_medium=referral&utm_campaign=homepage&utm_content=links)

* [Demo store](https://www.nopcommerce.com/demo?utm_source=github&utm_medium=referral&utm_campaign=demo_store&utm_content=links)
* [Download nopCommerce](https://www.nopcommerce.com/download-nopcommerce?utm_source=github&utm_medium=referral&utm_campaign=download_nop&utm_content=links)
* [Online course for developers](https://nopcommerce.com/training?utm_source=github&utm_medium=referral&utm_campaign=course&utm_content=links)
* [Feature list](https://www.nopcommerce.com/features?utm_source=github&utm_medium=referral&utm_campaign=features&utm_content=links)
* [Web API plugin](https://www.nopcommerce.com/web-api?utm_source=github&utm_medium=referral&utm_campaign=WebAPI&utm_content=links)
* [nopCommerce documentation](https://docs.nopcommerce.com?utm_source=github&utm_medium=referral&utm_campaign=documentation&utm_content=links)
* [Community forums](https://www.nopcommerce.com/boards?utm_source=github&utm_medium=referral&utm_campaign=forum&utm_content=links)
* [Premium support services](https://www.nopcommerce.com/nopcommerce-premium-support-services?utm_source=github&utm_medium=referral&utm_campaign=premium_support&utm_content=links)
* [Certified developer program](https://www.nopcommerce.com/certified-developer-program?utm_source=github&utm_medium=referral&utm_campaign=certified_developer&utm_content=links)
* [nopCommerce partners](https://www.nopcommerce.com/partners?utm_source=github&utm_medium=referral&utm_campaign=solution_partners&utm_content=links)

nopCommerce YouTube: [The Architecture behind the nopCommerce eCommerce Platform](https://www.youtube.com/watch?v=6gLbizzSA9o&list=PLnL_aDfmRHwtJmzeA7SxrpH3-XDY2ue0a)


### Earn with nopCommerce ###

60,000 stores worldwide are powered by nopCommerce, and 10,000 new stores open every year. nopCommerce [solution partners’ directory](https://www.nopcommerce.com/partners?utm_source=github&utm_medium=referral&utm_campaign=solution_partners&utm_content=text_become_partner) gets 80,000+ page views per year from store owners who are looking for a partner to build a store from scratch, migrate from another platform, or improve and customize an existing store.

Become a solution partner of nopCommerce and get new clients – [learn more](https://www.nopcommerce.com/become-partner?utm_source=github&utm_medium=referral&utm_campaign=become-partner&utm_content=learn_more).

Create a new graphical theme or develop a new plugin or integration and sell it on the nopCommerce [Marketplace](https://www.nopcommerce.com/marketplace?utm_source=github&utm_medium=referral&utm_campaign=marketplace&utm_content=text_sell_on_marketplace).


### Contribute ###

As a free and open-source project, we are very grateful to everyone who helps us to develop nopCommerce. Please find more details about the options and bonuses for contributors at [contribute page](https://www.nopcommerce.com/contribute?utm_source=github&utm_medium=referral&utm_campaign=contribute&utm_content=text).
