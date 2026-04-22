# Baostock.NET

A pure .NET 9 client for the [baostock](https://baostock.com/) China A-share market data service.

> **Status: Pre-alpha (scaffold).** Protocol reverse-engineering and core client are in active design. APIs will change.

## Why

The official `baostock` Python package is the only first-party client. Cross-language users (.NET, especially financial analytics tools on Windows) need a process-free, dependency-free integration. Baostock.NET re-implements the client-side socket protocol natively in C#.

## Goals

- **Native** — pure managed C#, no Python sidecar, no IPC, no embedded interpreter.
- **Faithful** — 1:1 coverage of the public Python API surface (login, K-line, fundamentals, macro, index constituents, calendar, etc.).
- **Idiomatic** — async/await throughout, strongly-typed result models, `IAsyncEnumerable<T>` streaming for large result sets.
- **Testable** — protocol layer separable from transport for unit testing without a live server.

## Project Layout

```
baostock.Net/
├── src/
│   └── Baostock.NET/              # the library
├── tests/
│   └── Baostock.NET.Tests/        # xUnit tests
├── reference/
│   └── baostock-python/           # snapshot of the upstream Python package
│                                  # (read-only, used for protocol reverse-engineering only)
├── Baostock.NET.sln
└── LICENSE                        # MIT
```

## Build

```powershell
dotnet build
dotnet test
```

Requires .NET 9 SDK.

## Roadmap

See the v0.5.0 integration plan in the consuming project repository for usage scope.

Internal milestones for this library:

1. Protocol decode (message framing, login handshake, query/response types).
2. `BaostockClient` — login/logout, basic query.
3. K-line: `query_history_k_data_plus`, `query_dividend_data`, `query_adjust_factor`.
4. Fundamentals: profitability / operation / growth / balance / cashflow / dupont (quarterly).
5. Macro: deposit/loan rate, RRR, M0/M1/M2, Shibor, CPI, PPI, GDP.
6. Index constituents: SSE50, HS300, ZZ500.
7. Reference: trading dates, all-stock listing, industry classification, security basic.
8. Performance forecast / express.

## Acknowledgements

- [baostock](https://baostock.com/) — the underlying free data service.
- The Python `baostock` package — reference implementation for protocol semantics.

## License

MIT — see [LICENSE](LICENSE).
