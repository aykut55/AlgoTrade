from imgui_bundle import imgui


class MenuBar:
    """Immediate-mode application menu for the ImGui plotter."""

    def __init__(self):
        self._callbacks = {}
        self._aboutVisible = False
        self._command = ""

    def set_callback(self, **callbacks):
        self._callbacks = callbacks

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
                if imgui.menu_item_simple("Reset All Views"):
                    self._call("on_reset_views")
                if imgui.menu_item_simple("Toggle Dark / Light"):
                    self._call("on_toggle_theme")
                imgui.end_menu()

            if imgui.begin_menu("Panels"):
                items = (
                    ("Left Menu", "on_toggle_left_menu"),
                    ("Pool Panel", "on_toggle_pool"),
                    ("Script Panel", "on_toggle_script"),
                    ("Console Panel", "on_toggle_console"),
                    ("Panel Manager", "on_manage_panels"),
                    ("Panel Manager 2", "on_manage_panels2"),
                    ("Reports", "on_toggle_reports"),
                    ("Measure", "on_toggle_measure"),
                    ("Data Manager", "on_manage_data"),
                )
                for label, callback in items:
                    if imgui.menu_item_simple(label):
                        self._call(callback)
                imgui.separator()
                for label, callback in (
                    ("Show All Plot Panels", "on_open_all"),
                    ("Collapse All Plot Panels", "on_collapse_all"),
                    ("Expand All Plot Panels", "on_expand_all"),
                    ("Hide All Plot Panels", "on_close_all"),
                ):
                    if imgui.menu_item_simple(label):
                        self._call(callback)
                imgui.separator()
                if imgui.menu_item_simple("Open Indicator Window"):
                    self._call("on_open_indicator")
                if imgui.menu_item_simple("Open Indicator Window 2"):
                    self._call("on_open_indicator_selector")
                imgui.end_menu()

            if imgui.begin_menu("Help"):
                if imgui.menu_item_simple("About"):
                    self._aboutVisible = True
                imgui.end_menu()

            imgui.separator()
            imgui.set_next_item_width(260)
            submitted, self._command = imgui.input_text(
                "##commandPalette", self._command,
                imgui.InputTextFlags_.enter_returns_true.value)
            if submitted:
                self._call("on_command_query", self._command)
                self._command = ""
            imgui.end_main_menu_bar()

        if self._aboutVisible:
            opened, self._aboutVisible = imgui.begin("About", self._aboutVisible)
            if opened:
                imgui.text("DearImGuiBundleDataPlotter")
                imgui.text("AlgoTrade interactive data plotter")
            imgui.end()

    def build(self):
        """Retained-mode compatibility; rendering happens per frame."""
        return self
