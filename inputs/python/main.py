def hello():
    print("Hello Python")


def print_data_info(date_times, dates, times,
                    opens, highs, lows, closes, volumes, lots,
                    sinyal_list, kar_zarar_list, bakiye_list,
                    getiri_list, komisyon_list, bakiye_net_list, getiri_net_list,
                    strategy_indicators=None,
                    title="AlgoTrade", periyot="1H"):

    n = len(closes)
    sep = "-" * 52

    print()
    print(sep)
    print(f"  {title}  |  {periyot}  |  {n} bar")
    print(sep)

    if n > 0:
        print(f"  Tarih araligi  : {dates[0]}  →  {dates[-1]}")
        print(f"  Close          : min={min(closes):.4f}  max={max(closes):.4f}  son={closes[-1]:.4f}")

    # Sinyal özeti
    buy_count  = sum(1 for v in sinyal_list if v > 0)
    sell_count = sum(1 for v in sinyal_list if v < 0)
    print(f"  Sinyaller      : Al={buy_count}  Sat={sell_count}")

    # Bakiye özeti
    if bakiye_list:
        print(f"  Bakiye         : baslangic={bakiye_list[0]:.2f}  son={bakiye_list[-1]:.2f}"
              f"  min={min(bakiye_list):.2f}  max={max(bakiye_list):.2f}")

    # Bakiye net özeti
    if bakiye_net_list:
        print(f"  Bakiye Net     : baslangic={bakiye_net_list[0]:.2f}  son={bakiye_net_list[-1]:.2f}"
              f"  min={min(bakiye_net_list):.2f}  max={max(bakiye_net_list):.2f}")

    # Komisyon özeti
    if komisyon_list:
        total_komisyon = sum(komisyon_list)
        print(f"  Komisyon       : toplam={total_komisyon:.2f}")

    # Getiri net özeti
    if getiri_net_list:
        total_net = sum(getiri_net_list)
        print(f"  Getiri Net     : toplam={total_net:.2f}")

    # Strategy indicators
    if strategy_indicators:
        keys = list(strategy_indicators.keys()) if hasattr(strategy_indicators, 'keys') else []
        print(f"  Indikatörler   : {', '.join(f'{k}({len(strategy_indicators[k])})' for k in keys)}")
    else:
        print(f"  Indikatörler   : (yok)")

    print(sep)
    print()


if __name__ == "__main__":
    hello()
