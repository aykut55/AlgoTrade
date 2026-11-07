import numpy as np
from imgui_bundle import implot


def toImVec4(rgba255, alphaOverride=None):
    """DPG renk formati (0-255 int RGBA tuple) -> implot.Spec'in bekledigi
    ImVec4Like (0-1 float RGBA)."""
    r, g, b, a = rgba255
    if alphaOverride is not None:
        a = alphaOverride * 255.0
    return (r / 255.0, g / 255.0, b / 255.0, a / 255.0)


def drawCandles(tagPrefix, xs, opens, highs, lows, closes, label="",
                bullColor=(0, 200, 90, 255), bearColor=(220, 70, 70, 255),
                bodyWidth=0.6):
    """ImPlot'ta (DPG'nin add_candle_series'i de DAHIL - DPG kendi C++
    tarafinda ayni sekilde fitil+govdeyi primitiflerden cizer) native bir
    candlestick widget'i YOK. Bu yuzden her bar icin fitil (plot_line) +
    govde (bogada govde-dikdortgeni outline'i plot_line, ayida dolu
    plot_shaded) elle cizilir - DPG'nin gorsel sonucunu (bull/bear renk,
    fitil+govde) birebir eslemek icin.

    tagPrefix cagiran icin BENZERSIZ olmali (implot item id'leri label_id'den
    turetiliyor) - panel+data+(run index varsa) ayirt edici olacak sekilde
    kurulmali (bkz. panelManager.py/tradeSignalRenderer.py cagri yerleri).
    "##" on-eki implot'un bu item'lari legend'de GOSTERMEMESINI saglar - tek
    bir legend girisi icin asagida ayrica gorunmez bir "proxy" cizgi eklenir.
    """
    n = min(len(xs), len(opens), len(highs), len(lows), len(closes))
    if n <= 0:
        return
    half = bodyWidth / 2.0
    bullCol = toImVec4(bullColor)
    bearCol = toImVec4(bearColor)

    if label:
        implot.plot_line(label, np.array([float(xs[0])]), np.array([float("nan")]),
                         spec=implot.Spec(line_color=bullCol, line_weight=1.0))

    for i in range(n):
        x, o, h, l, c = float(xs[i]), float(opens[i]), float(highs[i]), float(lows[i]), float(closes[i])
        bull = c >= o
        color = bullCol if bull else bearCol
        implot.plot_line(f"##{tagPrefix}_wick_{i}", np.array([x, x]), np.array([l, h]),
                         spec=implot.Spec(line_color=color, line_weight=1.0))
        top, bottom = (c, o) if bull else (o, c)
        if bull:
            bx = np.array([x - half, x + half, x + half, x - half, x - half])
            by = np.array([bottom, bottom, top, top, bottom])
            implot.plot_line(f"##{tagPrefix}_body_{i}", bx, by,
                             spec=implot.Spec(line_color=color, line_weight=1.0))
        else:
            implot.plot_shaded(f"##{tagPrefix}_body_{i}",
                               np.array([x - half, x + half]),
                               np.array([top, top]), np.array([bottom, bottom]),
                               spec=implot.Spec(fill_color=color, fill_alpha=0.8))
