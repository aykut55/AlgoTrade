def hello():
    print("Hello Python")

def print_data_info(trade_data) -> bool:
    from data_plotter import DataPlotter
    return DataPlotter(trade_data).plot()

def print_multiple_trader_data(trader_list) -> bool:
    from multiple_data_plotter import MultipleDataPlotter
    return MultipleDataPlotter(trader_list).plot()

if __name__ == "__main__":
    hello()
