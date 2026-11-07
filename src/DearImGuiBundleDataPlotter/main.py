import locale
import os
import subprocess
import sys

from imgui_bundle import imgui, immapp, hello_imgui

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from src.config.configManager import ConfigManager
from src.plotting.guiManager import GuiManager


class App:
    def __init__(self):
        try:
            locale.setlocale(locale.LC_TIME, "tr_TR.UTF-8")
        except locale.Error:
            try:
                locale.setlocale(locale.LC_TIME, "Turkish_Turkey.1254")
            except locale.Error:
                pass

        # TODO: DPG surumundeki _setupFont() (segoeui.ttf, Turkce karakterler icin)
        # henuz portlanmadi - imgui_bundle/hello_imgui font yuklemesi
        # (RunnerParams.callbacks.load_additional_fonts) ayri bir adim gerektiriyor,
        # MVP kapsaminda degil (bkz. plan).
        self.configManager = ConfigManager()
        self.guiManager = GuiManager(self.configManager)
        self.guiManager.build()

    def gui(self):
        self.guiManager.render()

    def run(self):
        subprocess.call("cls", shell=True)
        window = self.configManager.get("window")

        runnerParams = hello_imgui.RunnerParams()
        runnerParams.app_window_params.window_title = "DearImGuiBundleDataPlotter"
        runnerParams.app_window_params.window_geometry.size = [window["width"], window["height"]]
        runnerParams.imgui_window_params.default_imgui_window_type = \
            hello_imgui.DefaultImGuiWindowType.provide_full_screen_dock_space

        topSplit = hello_imgui.DockingSplit(
            "MainDockSpace", "TopDockSpace", imgui.Dir_.up, 0.15,
            imgui.DockNodeFlags_.no_resize | imgui.DockNodeFlags_.auto_hide_tab_bar)
        leftSplit = hello_imgui.DockingSplit(
            "MainDockSpace", "LeftDockSpace", imgui.Dir_.left, 0.18)
        rightSplit = hello_imgui.DockingSplit(
            "MainDockSpace", "RightDockSpace", imgui.Dir_.right, 0.32)
        consoleSplit = hello_imgui.DockingSplit(
            "RightDockSpace", "ConsoleDockSpace", imgui.Dir_.down, 0.35)

        topWindow = hello_imgui.DockableWindow(
            "Toolbar", "TopDockSpace", self.guiManager.renderTopDockContent, True, False)
        topWindow.imgui_window_flags = (imgui.WindowFlags_.no_scrollbar |
                                        imgui.WindowFlags_.no_scroll_with_mouse)
        mainWindow = hello_imgui.DockableWindow(
            "Charts", "MainDockSpace", self.guiManager.renderChartsDockContent, True, False)
        leftWindow = hello_imgui.DockableWindow(
            "Left Menu", "LeftDockSpace", self.guiManager.leftMenuPanel.renderDockContent,
            self.guiManager.leftMenuPanel.isVisible(), True)
        scriptWindow = hello_imgui.DockableWindow(
            "Script Panel", "RightDockSpace", self.guiManager.scriptPanel.renderDockContent,
            self.guiManager.scriptPanel.getVisible(), True)
        scriptWindow.focus_window_at_next_frame = True
        dataManagerWindow = hello_imgui.DockableWindow(
            "Data Manager", "RightDockSpace", self.guiManager.dataManager.renderDockContent,
            self.guiManager.dataManager.getVisible(), True)
        poolWindow = hello_imgui.DockableWindow(
            "Pool", "RightDockSpace", self.guiManager.poolPanel.renderDockContent,
            self.guiManager.poolPanel.getVisible(), True)
        panelManagerWindow = hello_imgui.DockableWindow(
            "Panel Manager", "RightDockSpace", self.guiManager.panelManagerWindow.renderDockContent,
            self.guiManager.panelManagerWindow.getVisible(), True)
        dataManagerWindow.remember_is_visible = False
        poolWindow.remember_is_visible = False
        panelManagerWindow.remember_is_visible = False
        consoleWindow = hello_imgui.DockableWindow(
            "Console Panel", "ConsoleDockSpace", self.guiManager.consolePanel.renderDockContent,
            self.guiManager.consolePanel.getVisible(), True)

        runnerParams.docking_params.docking_splits = [topSplit, leftSplit, rightSplit, consoleSplit]
        runnerParams.docking_params.dockable_windows = [
            topWindow, mainWindow, leftWindow, scriptWindow, dataManagerWindow,
            poolWindow, panelManagerWindow, consoleWindow]
        runnerParams.docking_params.layout_condition = \
            hello_imgui.DockingLayoutCondition.application_start

        # pybind std::vector atamasinda DockableWindow nesneleri listeye
        # kopyalanir. Panels menu callback'leri ilk yerel nesneyi degil,
        # DockingParams icindeki gercek pencere state'ini degistirmelidir.
        actualLeftWindow = runnerParams.docking_params.dockable_window_of_name("Left Menu")
        actualScriptWindow = runnerParams.docking_params.dockable_window_of_name("Script Panel")
        actualConsoleWindow = runnerParams.docking_params.dockable_window_of_name("Console Panel")
        actualDataManagerWindow = runnerParams.docking_params.dockable_window_of_name("Data Manager")
        actualPoolWindow = runnerParams.docking_params.dockable_window_of_name("Pool")
        actualPanelManagerWindow = runnerParams.docking_params.dockable_window_of_name("Panel Manager")
        self.guiManager.leftMenuPanel.setDockWindow(actualLeftWindow)
        self.guiManager.scriptPanel.setDockWindow(actualScriptWindow)
        self.guiManager.dataManager.setDockWindow(actualDataManagerWindow)
        self.guiManager.poolPanel.setDockWindow(actualPoolWindow)
        self.guiManager.panelManagerWindow.setDockWindow(actualPanelManagerWindow)
        self.guiManager.consolePanel.setDockWindow(actualConsoleWindow)

        addOns = immapp.AddOnsParams(with_implot=True)
        immapp.run(runnerParams, addOns)


if __name__ == "__main__":
    app = App()
    app.run()
