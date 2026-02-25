def hello():
    print("Hello Python")

def print_data_info(trade_data) -> bool:
    from data_plotter import DataPlotter
    return DataPlotter(trade_data).plot()

if __name__ == "__main__":
    hello()
