import os
import subprocess
import sys

from imgui_bundle import imgui, immapp, hello_imgui

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from src.leftMenuPanel import LeftMenuPanel
from src.configPanel import ConfigPanel
from src.menuBar import MenuBar
from src.activeSettingsPanel import ActiveSettingsPanel

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
INPUTS_DIR = os.path.join(PROJECT_ROOT, "inputs")
APPCONFIG_DIR = os.path.join(INPUTS_DIR, "configs", "AppConfig")

RUN_PREVIEWS = [
    ("SingleTrader", "SingleTraderRunPreview.json"),
    ("SingleTraderOpt", "SingleTraderOptRunPreview.json"),
    ("MultipleTrader", "MultipleTraderRunPreview.json"),
    ("ConfirmingSingleTrader", "ConfirmingSingleTraderRunPreview.json"),
    ("ConfirmingMultipleTrader", "ConfirmingMultipleTraderRunPreview.json"),
]


class App:
    def __init__(self):
        self.leftMenuPanel = LeftMenuPanel(INPUTS_DIR)
        self.leftMenuPanel.setOnFileSelected(self._onFileSelected)
        self.configPanel = ConfigPanel()
        self.activeSettingsPanels = [
            (title, ActiveSettingsPanel(os.path.join(APPCONFIG_DIR, fileName)))
            for title, fileName in RUN_PREVIEWS
        ]
        self.menuBar = MenuBar()
        self.menuBar.set_callback(
            on_exit=self._onExit,
            on_toggle_theme=self.toggleTheme,
            on_toggle_panel=self._togglePanel,
        )
        self._isDarkTheme = True
        self._themeApplied = False
        self._panelWindows = {}

    def _onFileSelected(self, path):
        self.configPanel.setSelectedFile(path)

    def _onExit(self):
        hello_imgui.get_runner_params().app_shall_exit = True

    def toggleTheme(self):
        self._isDarkTheme = not self._isDarkTheme
        if self._isDarkTheme:
            imgui.style_colors_dark()
        else:
            imgui.style_colors_light()

    def _togglePanel(self, title):
        window = self._panelWindows.get(title)
        if window is not None:
            window.is_visible = not window.is_visible

    def _applyThemeForFrame(self):
        if not self._themeApplied:
            if self._isDarkTheme:
                imgui.style_colors_dark()
            else:
                imgui.style_colors_light()
            self._themeApplied = True

    def renderTopDockContent(self):
        self._applyThemeForFrame()
        self.menuBar.render()

    def run(self):
        subprocess.call("cls", shell=True)

        runnerParams = hello_imgui.RunnerParams()
        runnerParams.app_window_params.window_title = "AlgoTrade Config Editor"
        runnerParams.app_window_params.window_geometry.size = [1000, 700]
        runnerParams.imgui_window_params.default_imgui_window_type = \
            hello_imgui.DefaultImGuiWindowType.provide_full_screen_dock_space

        topSplit = hello_imgui.DockingSplit(
            "MainDockSpace", "TopDockSpace", imgui.Dir_.up, 0.07,
            imgui.DockNodeFlags_.no_resize | imgui.DockNodeFlags_.auto_hide_tab_bar)
        leftSplit = hello_imgui.DockingSplit(
            "MainDockSpace", "LeftDockSpace", imgui.Dir_.left, 0.22)
        rightSplit = hello_imgui.DockingSplit(
            "MainDockSpace", "RightDockSpace", imgui.Dir_.right, 0.28)

        topWindow = hello_imgui.DockableWindow(
            "Toolbar", "TopDockSpace", self.renderTopDockContent, True, False)
        topWindow.imgui_window_flags = (imgui.WindowFlags_.no_scrollbar |
                                        imgui.WindowFlags_.no_scroll_with_mouse)
        mainWindow = hello_imgui.DockableWindow(
            "Config", "MainDockSpace", self.configPanel.renderDockContent, True, False)
        leftWindow = hello_imgui.DockableWindow(
            "Left Menu", "LeftDockSpace", self.leftMenuPanel.renderDockContent, True, True)
        rightWindows = [
            hello_imgui.DockableWindow(title, "RightDockSpace", panel.renderDockContent, True, False)
            for title, panel in self.activeSettingsPanels
        ]

        runnerParams.docking_params.docking_splits = [topSplit, leftSplit, rightSplit]
        runnerParams.docking_params.dockable_windows = [topWindow, mainWindow, leftWindow] + rightWindows
        runnerParams.docking_params.layout_condition = \
            hello_imgui.DockingLayoutCondition.application_start

        panelTitles = ["Left Menu", "Config"] + [title for title, _ in self.activeSettingsPanels]
        self._panelWindows = {
            title: runnerParams.docking_params.dockable_window_of_name(title)
            for title in panelTitles
        }
        self.menuBar.set_panel_titles(panelTitles)

        addOns = immapp.AddOnsParams(with_implot=True)
        immapp.run(runnerParams, addOns)


if __name__ == "__main__":
    app = App()
    app.run()
