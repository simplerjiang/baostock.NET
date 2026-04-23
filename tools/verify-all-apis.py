import baostock as bs
import json

def show(name, rs, max_rows=3):
    """打印 query 结果的前 max_rows 行"""
    print(f"\n{'='*60}")
    print(f"API: {name}")
    print(f"error_code: {rs.error_code}, error_msg: {rs.error_msg}")
    if hasattr(rs, 'fields') and rs.fields:
        print(f"fields: {rs.fields}")
    rows = []
    while (rs.error_code == '0') and rs.next():
        rows.append(rs.get_row_data())
    print(f"total rows: {len(rows)}")
    for i, row in enumerate(rows[:max_rows]):
        print(f"  row[{i}]: {row}")
    if len(rows) > max_rows:
        print(f"  ... ({len(rows) - max_rows} more rows)")
    return len(rows)

bs.login()
total_apis = 0
total_rows = 0

# 1. K线
rs = bs.query_history_k_data_plus("sh.600000", "date,code,open,high,low,close,volume", 
    start_date="2024-01-02", end_date="2024-01-10", frequency="d", adjustflag="2")
n = show("query_history_k_data_plus", rs)
total_apis += 1; total_rows += n

# 2-4. 板块
for name, fn in [
    ("query_stock_industry", lambda: bs.query_stock_industry(code="sh.600000", date="2024-01-02")),
    ("query_hs300_stocks", lambda: bs.query_hs300_stocks(date="2024-01-02")),
    ("query_sz50_stocks", lambda: bs.query_sz50_stocks(date="2024-01-02")),
    ("query_zz500_stocks", lambda: bs.query_zz500_stocks(date="2024-01-02")),
]:
    rs = fn()
    n = show(name, rs)
    total_apis += 1; total_rows += n

# 5-12. 季频
for name, fn in [
    ("query_dividend_data", lambda: bs.query_dividend_data(code="sh.600000", year="2023", yearType="report")),
    ("query_adjust_factor", lambda: bs.query_adjust_factor(code="sh.600000", start_date="2024-01-01", end_date="2024-01-31")),
    ("query_profit_data", lambda: bs.query_profit_data(code="sh.600000", year=2023, quarter=2)),
    ("query_operation_data", lambda: bs.query_operation_data(code="sh.600000", year=2023, quarter=2)),
    ("query_growth_data", lambda: bs.query_growth_data(code="sh.600000", year=2023, quarter=2)),
    ("query_dupont_data", lambda: bs.query_dupont_data(code="sh.600000", year=2023, quarter=2)),
    ("query_balance_data", lambda: bs.query_balance_data(code="sh.600000", year=2023, quarter=2)),
    ("query_cash_flow_data", lambda: bs.query_cash_flow_data(code="sh.600000", year=2023, quarter=2)),
]:
    rs = fn()
    n = show(name, rs)
    total_apis += 1; total_rows += n

# 13-14. 公告
for name, fn in [
    ("query_performance_express_report", lambda: bs.query_performance_express_report(code="sh.600000", start_date="2023-01-01", end_date="2023-12-31")),
    ("query_forecast_report", lambda: bs.query_forecast_report(code="sh.600000", start_date="2023-01-01", end_date="2023-12-31")),
]:
    rs = fn()
    n = show(name, rs)
    total_apis += 1; total_rows += n

# 15-17. 元数据
for name, fn in [
    ("query_trade_dates", lambda: bs.query_trade_dates(start_date="2024-01-01", end_date="2024-01-10")),
    ("query_all_stock", lambda: bs.query_all_stock(day="2024-01-02")),
    ("query_stock_basic", lambda: bs.query_stock_basic(code="sh.600000")),
]:
    rs = fn()
    n = show(name, rs)
    total_apis += 1; total_rows += n

# 18-22. 宏观
for name, fn in [
    ("query_deposit_rate_data", lambda: bs.query_deposit_rate_data(start_date="2023-01-01", end_date="2023-12-31")),
    ("query_loan_rate_data", lambda: bs.query_loan_rate_data(start_date="2023-01-01", end_date="2023-12-31")),
    ("query_required_reserve_ratio_data", lambda: bs.query_required_reserve_ratio_data(start_date="2023-01-01", end_date="2023-12-31")),
    ("query_money_supply_data_month", lambda: bs.query_money_supply_data_month(start_date="2023-01", end_date="2023-06")),
    ("query_money_supply_data_year", lambda: bs.query_money_supply_data_year(start_date="2020", end_date="2023")),
]:
    rs = fn()
    n = show(name, rs)
    total_apis += 1; total_rows += n

bs.logout()

print(f"\n{'='*60}")
print(f"SUMMARY: {total_apis} APIs tested, {total_rows} total rows returned")
print(f"{'='*60}")
