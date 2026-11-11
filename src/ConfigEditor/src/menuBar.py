from imgui_bundle import imgui


class MenuBar:
    """Immediate-mode application menu, DearImGuiBundleDataPlotter'daki menuBar.py'nin kisaltilmis hali."""

    def __init__(self):
        self._callbacks = {}
        self._panelTitles = []

    def set_callback(self, **callbacks):
        self._callbacks = callbacks

    def set_panel_titles(self, titles):
        self._panelTitles = titles

    def _call(self, name, *args):
        callback = self._callbacks.get(name)
        if callback:
            callback(*args)

    def render(self):
        if imgui.begin_main_menu_bar():
            if imgui.begin_menu("File"):
                if imgui.menu_item_simple("Exit"):
                    self._call("on_exit")
                imgui.end_menu()

            if imgui.begin_menu("View"):
                if imgui.menu_item_simple("Toggle Dark / Light"):
                    self._call("on_toggle_theme")
                imgui.end_menu()

            if imgui.begin_menu("Panels"):
                for title in self._panelTitles:
                    if imgui.menu_item_simple(title):
                        self._call("on_toggle_panel", title)
                imgui.end_menu()

            imgui.end_main_menu_bar()
