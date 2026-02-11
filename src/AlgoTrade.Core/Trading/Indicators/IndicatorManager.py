'''
Technical Indicator Manager for algorithmic trading system.

This module contains the CIndicatorManager class which provides comprehensive
technical indicator calculations including moving averages, RSI, MACD, and more.
'''

from typing import List, Dict, Optional, Union, Tuple, Any
import numpy as np
import pandas as pd
from enum import Enum
from dataclasses import dataclass, field
from functools import lru_cache
import warnings

from src.Base import CBase

# Type aliases
NumericType = Union[int, float, np.number]
NumericList = Union[List[NumericType], np.ndarray]


class MAMethod(Enum):
    """Moving Average calculation methods."""
    SIMPLE = "Simple"
    EXPONENTIAL = "Exp" 
    WEIGHTED = "Weighted"
    HULL = "HullMA"
    TRIANGULAR = "Triangular"
    VARIABLE = "Variable"
    VOLUME = "Volume"
    WILDER = "Wilder"
    ZERO_LAG = "ZeroLag"
    TIME_SERIES = "TimeSeries"
    DEMA = "DEMA"
    TEMA = "TEMA"
    VWMA = "VWMA"
    SMMA = "SMMA"
    LSMA = "LSMA"
    KAMA = "KAMA"
    FRAMA = "FRAMA"
    VIDYA = "VIDYA"
    ZLEMA = "ZLEMA"
    T3 = "T3"
    ALMA = "ALMA"
    JMA = "JMA"
    MAMA = "MAMA"
    MCGINLEY = "MCGINLEY"
    VAMA = "VAMA"
    COVWMA = "COVWMA"
    COVWEMA = "COVWEMA"
    FAMA = "FAMA"
    SRWMA = "SRWMA"
    SWMA = "SWMA"
    EVWMA = "EVWMA"
    REGMA = "REGMA"
    REMA = "REMA"
    REPMA = "REPMA"
    RSIMA = "RSIMA"
    ETMA = "ETMA"
    TREMA = "TREMA"
    TRSMA = "TRSMA"
    THMA = "THMA"
    DWMA = "DWMA"
    TWMA = "TWMA"
    DVWMA = "DVWMA"
    TVWMA = "TVWMA"
    DHULL = "DHULL"
    THULL = "THULL"
    DZLEMA = "DZLEMA"
    TZLEMA = "TZLEMA"
    DSSMA = "DSSMA"
    TSSMA = "TSSMA"
    DSMMA = "DSMMA"
    TSMMA = "TSMMA"
    DSMA = "DSMA"
    TSMA = "TSMA"
    ADEMA = "ADEMA"
    EDMA = "EDMA"
    EDSMA = "EDSMA"
    AHMA = "AHMA"
    EHMA = "EHMA"
    ALSMA = "ALSMA"
    AARMA = "AARMA"
    MCMA = "MCMA"
    LEOMA = "LEOMA"
    CMA = "CMA"
    CORMA = "CORMA"
    AUTOL = "AUTOL"
    MEDIAN = "MEDIAN"
    GMA = "GMA"
    XEMA = "XEMA"
    ZSMA = "ZSMA"


@dataclass
class IndicatorConfig:
    """Configuration container for indicators."""
    
    # Common MA periods (Fibonacci sequence + common numbers)
    fibonacci_periods: List[int] = field(default_factory=lambda: [3, 5, 8, 13, 21, 34, 55, 89, 144, 233])
    common_periods: List[int] = field(default_factory=lambda: [5, 10, 15, 20, 30, 45, 50, 100, 200, 500, 1000])
    
    # Supported MA methods
    ma_methods: List[str] = field(default_factory=lambda: [method.value for method in MAMethod])
    
    # Cache size for performance
    cache_size: int = 128


class CIndicatorManager(CBase):
    """
    Technical Indicator Manager providing comprehensive indicator calculations.
    
    Features:
    - Multiple Moving Average types (Simple, EMA, Hull, etc.)
    - RSI (Relative Strength Index)
    - MACD (Moving Average Convergence Divergence)
    - Stochastic oscillators
    - Bulk calculation support
    - Performance optimization with caching
    """
    
    def __init__(self, config: Optional[IndicatorConfig] = None):
        """
        Initialize the indicator manager.
        
        Args:
            config: Optional configuration for indicators
        """
        super().__init__()
        self.config = config or IndicatorConfig()
        
        # Storage for calculated indicators
        self.ma_cache: Dict[str, np.ndarray] = {}
        self.rsi_cache: Dict[str, np.ndarray] = {}
        self.macd_cache: Dict[str, np.ndarray] = {}
        
        # Parameter tracking
        self.ma_params_list: List[str] = []
        self.ma_list: List[np.ndarray] = []
        self.ma_dictionary: Dict[str, np.ndarray] = {}
        
        self.rsi_params_list: List[str] = []
        self.rsi_list: List[np.ndarray] = []
        
        self.macd_params_list: List[str] = []
        self.macd_list: List[np.ndarray] = []
    
    def initialize(self, EpochTime, DateTime, Date, Time, Open, High, Low, Close, Volume, Lot):
        """
        Initialize the indicator manager with market data.
        
        Args:
            EpochTime: Epoch time data
            DateTime: DateTime data
            Date: Date data
            Time: Time data
            Open: Opening prices
            High: High prices
            Low: Low prices
            Close: Closing prices
            Volume: Volume data
            Lot: Lot data
            
        Returns:
            Self for method chaining
        """
        self.set_data(EpochTime, DateTime, Date, Time, Open, High, Low, Close, Volume, Lot)
        return self
    
    def reset(self) -> 'CIndicatorManager':
        """
        Reset all cached indicators and parameters.

        Returns:
            Self for method chaining
        """
        # Clear caches
        self.ma_cache.clear()
        self.rsi_cache.clear()
        self.macd_cache.clear()
        
        # Clear parameter lists
        self.ma_params_list.clear()
        self.ma_list.clear()
        self.ma_dictionary.clear()
        
        self.rsi_params_list.clear()
        self.rsi_list.clear()
        
        self.macd_params_list.clear()
        self.macd_list.clear()
        
        return self
    
    # ==================== Moving Average Calculations ====================
    
    def calculate_sma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Simple Moving Average.
        
        Args:
            source: Price series
            period: Calculation period
            
        Returns:
            SMA values as numpy array
        """
        if period <= 0:
            raise ValueError("Period must be positive")
        
        source_array = np.array(source, dtype=np.float64)
        if len(source_array) < period:
            return np.full(len(source_array), np.nan)
        
        # Use pandas rolling for efficient calculation
        series = pd.Series(source_array)
        sma = series.rolling(window=period, min_periods=period).mean()
        
        return sma.values
    
    def calculate_ema(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Exponential Moving Average.
        
        Args:
            source: Price series
            period: Calculation period
            
        Returns:
            EMA values as numpy array
        """
        if period <= 0:
            raise ValueError("Period must be positive")
        
        source_array = np.array(source, dtype=np.float64)
        if len(source_array) == 0:
            return np.array([])
        
        # Calculate EMA using pandas for efficiency
        series = pd.Series(source_array)
        ema = series.ewm(span=period, adjust=False).mean()
        
        return ema.values
    
    def calculate_wma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Weighted Moving Average.
        
        Args:
            source: Price series
            period: Calculation period
            
        Returns:
            WMA values as numpy array
        """
        if period <= 0:
            raise ValueError("Period must be positive")
        
        source_array = np.array(source, dtype=np.float64)
        if len(source_array) < period:
            return np.full(len(source_array), np.nan)
        
        # Create weights (1, 2, 3, ..., period)
        weights = np.arange(1, period + 1, dtype=np.float64)
        weights = weights / weights.sum()
        
        # Calculate WMA using convolution
        wma = np.convolve(source_array, weights[::-1], mode='valid')
        
        # Pad with NaN for the initial period
        result = np.full(len(source_array), np.nan)
        result[period-1:] = wma
        
        return result
    
    def calculate_hull_ma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Hull Moving Average.
        
        Args:
            source: Price series
            period: Calculation period
            
        Returns:
            Hull MA values as numpy array
        """
        if period <= 0:
            raise ValueError("Period must be positive")
        
        source_array = np.array(source, dtype=np.float64)
        
        # Hull MA formula: WMA(2*WMA(n/2) - WMA(n), sqrt(n))
        half_period = max(1, period // 2)
        sqrt_period = max(1, int(np.sqrt(period)))
        
        wma_half = self.calculate_wma(source_array, half_period)
        wma_full = self.calculate_wma(source_array, period)
        
        # Calculate 2*WMA(n/2) - WMA(n)
        hull_source = 2 * wma_half - wma_full
        
        # Apply WMA with sqrt(n) period
        hull_ma = self.calculate_wma(hull_source, sqrt_period)
        
        return hull_ma

    def calculate_dema(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Double Exponential Moving Average.
        
        Args:
            source: Price series
            period: Calculation period
            
        Returns:
            DEMA values as numpy array
        """
        ema1 = self.calculate_ema(source, period)
        ema2 = self.calculate_ema(ema1, period)
        dema = 2 * ema1 - ema2
        return dema

    def calculate_tema(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Triple Exponential Moving Average.
        
        Args:
            source: Price series
            period: Calculation period
            
        Returns:
            TEMA values as numpy array
        """
        ema1 = self.calculate_ema(source, period)
        ema2 = self.calculate_ema(ema1, period)
        ema3 = self.calculate_ema(ema2, period)
        tema = 3 * (ema1 - ema2) + ema3
        return tema

    def calculate_vwma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Volume Weighted Moving Average.
        
        Args:
            source: Price series
            period: Calculation period
            
        Returns:
            VWMA values as numpy array
        """
        source_array = np.array(source, dtype=np.float64)
        volume_array = np.array(self.Volume, dtype=np.float64)
        
        if len(source_array) < period:
            return np.full(len(source_array), np.nan)
            
        price_volume = source_array * volume_array
        
        price_volume_series = pd.Series(price_volume)
        volume_series = pd.Series(volume_array)
        
        sum_price_volume = price_volume_series.rolling(window=period, min_periods=period).sum()
        sum_volume = volume_series.rolling(window=period, min_periods=period).sum()
        
        vwma = sum_price_volume / sum_volume
        
        return vwma.values

    def calculate_lsma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Least Squares Moving Average (Linear Regression).
        
        Args:
            source: Price series
            period: Calculation period
            
        Returns:
            LSMA values as numpy array
        """
        source_array = np.array(source, dtype=np.float64)
        if len(source_array) < period:
            return np.full(len(source_array), np.nan)
            
        lsma = np.full(len(source_array), np.nan)
        x = np.arange(period)
        
        for i in range(len(source_array) - period + 1):
            y = source_array[i:i+period]
            if np.any(np.isnan(y)):
                continue
            slope, intercept = np.polyfit(x, y, 1)
            lsma[i + period - 1] = intercept + slope * (period - 1)
            
        return lsma

    def calculate_kama(self, source: NumericList, period: int, fast_period: int = 2, slow_period: int = 30) -> np.ndarray:
        """
        Calculate Kaufman's Adaptive Moving Average.
        
        Args:
            source: Price series
            period: KAMA period
            fast_period: Fast EMA period for smoothing constant
            slow_period: Slow EMA period for smoothing constant
            
        Returns:
            KAMA values as numpy array
        """
        source_series = pd.Series(source)
        
        change = (source_series - source_series.shift(period)).abs()
        volatility = (source_series - source_series.shift(1)).abs().rolling(window=period).sum()
        
        er = change / volatility
        er = er.fillna(0)
        
        fast_alpha = 2 / (fast_period + 1)
        slow_alpha = 2 / (slow_period + 1)
        
        sc = (er * (fast_alpha - slow_alpha) + slow_alpha) ** 2
        
        kama = np.full(len(source), np.nan)
        
        # Set the first KAMA value to the first source value
        if len(source) > 0:
            kama[0] = source[0]
            
        for i in range(1, len(source)):
            if np.isnan(kama[i-1]):
                 kama[i] = source[i]
            else:
                kama[i] = kama[i-1] + sc[i] * (source[i] - kama[i-1])
                
        return kama

    def calculate_vidya(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Variable Index Dynamic Average.
        
        Args:
            source: Price series
            period: VIDYA period
            
        Returns:
            VIDYA values as numpy array
        """
        source_series = pd.Series(source)
        mom = source_series.diff()
        up_sum = mom.where(mom > 0, 0).rolling(window=period).sum()
        down_sum = -mom.where(mom < 0, 0).rolling(window=period).sum()
        
        # Avoid division by zero
        cmo = (up_sum - down_sum) / (up_sum + down_sum)
        cmo = cmo.fillna(0).abs()
        
        alpha = 2 / (period + 1)
        vidya = np.full(len(source), np.nan)
        
        if len(source) > 0:
            vidya[0] = source[0]
            
        for i in range(1, len(source)):
            if np.isnan(vidya[i-1]):
                vidya[i] = source[i]
            else:
                vidya[i] = source[i] * alpha * cmo[i] + vidya[i-1] * (1 - alpha * cmo[i])
                
        return vidya

    def calculate_zlema(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Zero-Lag Exponential Moving Average.
        
        Args:
            source: Price series
            period: ZLEMA period
            
        Returns:
            ZLEMA values as numpy array
        """
        lag = (period - 1) // 2
        source_series = pd.Series(source)
        zlema_data = source_series + (source_series - source_series.shift(lag))
        
        zlema = self.calculate_ema(zlema_data, period)
        
        return zlema

    def calculate_t3(self, source: NumericList, period: int, v_factor: float = 0.7) -> np.ndarray:
        """
        Calculate Tillson T3 Moving Average.
        
        Args:
            source: Price series
            period: T3 period
            v_factor: Volume factor
            
        Returns:
            T3 values as numpy array
        """
        ema1 = self.calculate_ema(source, period)
        ema2 = self.calculate_ema(ema1, period)
        ema3 = self.calculate_ema(ema2, period)
        ema4 = self.calculate_ema(ema3, period)
        ema5 = self.calculate_ema(ema4, period)
        ema6 = self.calculate_ema(ema5, period)
        
        c1 = -1 * v_factor**3
        c2 = 3 * v_factor**2 + 3 * v_factor**3
        c3 = -6 * v_factor**2 - 3 * v_factor - 3 * v_factor**3
        c4 = 1 + 3 * v_factor + v_factor**3 + 3 * v_factor**2
        
        t3 = c1 * ema6 + c2 * ema5 + c3 * ema4 + c4 * ema3
        
        return t3

    def calculate_alma(self, source: NumericList, period: int, sigma: float = 6.0, offset: float = 0.85) -> np.ndarray:
        """
        Calculate Arnaud Legoux Moving Average.
        
        Args:
            source: Price series
            period: ALMA period
            sigma: Sigma for Gaussian distribution
            offset: Offset for Gaussian distribution
            
        Returns:
            ALMA values as numpy array
        """
        source_array = np.array(source, dtype=np.float64)
        if len(source_array) < period:
            return np.full(len(source_array), np.nan)
            
        m = offset * (period - 1)
        s = period / sigma
        
        w = np.exp(-((np.arange(period) - m) ** 2) / (2 * s * s))
        w /= np.sum(w)
        
        alma = np.convolve(source_array, w, mode='valid')
        
        result = np.full(len(source_array), np.nan)
        result[period-1:] = alma
        
        return result

    def calculate_jma(self, source: NumericList, period: int, phase: float = 0, power: float = 2) -> np.ndarray:
        """
        Calculate Jurik Moving Average.
        
        Args:
            source: Price series
            period: JMA period
            phase: Phase parameter
            power: Power parameter
            
        Returns:
            JMA values as numpy array
        """
        source_array = np.array(source, dtype=np.float64)
        if len(source_array) < period:
            return np.full(len(source_array), np.nan)

        phase_ratio = 0.5 if phase < -100 else 2.5 if phase > 100 else phase / 100 + 1.5
        beta = 0.45 * (period - 1) / (0.45 * (period - 1) + 2)
        alpha = beta ** power
        
        ma1 = np.zeros_like(source_array)
        det0 = np.zeros_like(source_array)
        jma = np.zeros_like(source_array)
        
        ma1[0] = source_array[0]
        for i in range(1, len(source_array)):
            ma1[i] = (1 - alpha) * source_array[i] + alpha * ma1[i-1]
            
        det0[0] = 0
        for i in range(1, len(source_array)):
            det0[i] = (source_array[i] - ma1[i]) * (1 - beta) + beta * det0[i-1]
            
        ma2 = ma1 + phase_ratio * det0
        
        det1 = np.zeros_like(source_array)
        jma[0] = ma2[0]
        for i in range(1, len(source_array)):
            det1[i] = (ma2[i] - jma[i-1]) * ((1 - alpha)**2) + (alpha**2) * det1[i-1]
            jma[i] = jma[i-1] + det1[i]
            
        return jma

    def calculate_frama(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Fractal Adaptive Moving Average. (Not yet implemented)
        """
        warnings.warn("FRAMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_mama(self, source: NumericList, fast_limit: float = 0.5, slow_limit: float = 0.05) -> np.ndarray:
        """
        Calculate MESA Adaptive Moving Average. (Not yet implemented)
        """
        warnings.warn("MAMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_mcginley(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate McGinley Dynamic. (Not yet implemented)
        """
        warnings.warn("McGinley Dynamic is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_vama(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Volatility Adjusted Moving Average. (Not yet implemented)
        """
        warnings.warn("VAMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_covwma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Coefficient of Variation Weighted Moving Average. (Not yet implemented)
        """
        warnings.warn("COVWMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_covwema(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Coefficient of Variation Weighted Exponential Moving Average. (Not yet implemented)
        """
        warnings.warn("COVWEMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_fama(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Following Adaptive Moving Average. (Not yet implemented)
        """
        warnings.warn("FAMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_srwma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Square Root Weighted Moving Average. (Not yet implemented)
        """
        warnings.warn("SRWMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_swma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Symmetrically (Sine-) Weighted Moving Average. (Not yet implemented)
        """
        warnings.warn("SWMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_evwma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Elastic Volume Weighted MA. (Not yet implemented)
        """
        warnings.warn("EVWMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_regma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Regularized Exponential Moving Average. (Not yet implemented)
        """
        warnings.warn("REGMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_rema(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Range EMA. (Not yet implemented)
        """
        warnings.warn("REMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_repma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Repulsion MA. (Not yet implemented)
        """
        warnings.warn("REPMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_rsima(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate RSI-based MA. (Not yet implemented)
        """
        warnings.warn("RSIMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_etma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Exponential Triangular MA. (Not yet implemented)
        """
        warnings.warn("ETMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_trema(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Triangular Exponential MA. (Not yet implemented)
        """
        warnings.warn("TREMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_trsma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Triangular Simple MA. (Not yet implemented)
        """
        warnings.warn("TRSMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_thma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Triple Hull MA. (Not yet implemented)
        """
        warnings.warn("THMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_dwma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Double WMA. (Not yet implemented)
        """
        warnings.warn("DWMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_twma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Triple WMA. (Not yet implemented)
        """
        warnings.warn("TWMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_dvwma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Double VWMA. (Not yet implemented)
        """
        warnings.warn("DVWMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_tvwma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Triple VWMA. (Not yet implemented)
        """
        warnings.warn("TVWMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_dhull(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Double Hull MA. (Not yet implemented)
        """
        warnings.warn("DHULL is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_thull(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Triple Hull MA. (Not yet implemented)
        """
        warnings.warn("THULL is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_dzlema(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Double ZLEMA. (Not yet implemented)
        """
        warnings.warn("DZLEMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_tzlema(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Triple ZLEMA. (Not yet implemented)
        """
        warnings.warn("TZLEMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_dssma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Double SSMA. (Not yet implemented)
        """
        warnings.warn("DSSMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_tssma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Triple SSMA. (Not yet implemented)
        """
        warnings.warn("TSSMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_dsmma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Double SMMA. (Not yet implemented)
        """
        warnings.warn("DSMMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_tsmma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Triple SMMA. (Not yet implemented)
        """
        warnings.warn("TSMMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_dsma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Double SMA. (Not yet implemented)
        """
        warnings.warn("DSMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_tsma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Triple SMA. (Not yet implemented)
        """
        warnings.warn("TSMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_adema(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Alpha-Decreasing EMA. (Not yet implemented)
        """
        warnings.warn("ADEMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_edma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Exponentially Deviating MA. (Not yet implemented)
        """
        warnings.warn("EDMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_edsma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Ehlers Dynamic Smoothed MA. (Not yet implemented)
        """
        warnings.warn("EDSMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_ahma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Ahrens MA. (Not yet implemented)
        """
        warnings.warn("AHMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_ehma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Exponential Hull MA. (Not yet implemented)
        """
        warnings.warn("EHMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_alsma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Adaptive Least Squares MA. (Not yet implemented)
        """
        warnings.warn("ALSMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_aarma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Adaptive Autonomous Recursive MA. (Not yet implemented)
        """
        warnings.warn("AARMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_mcma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate McNicholl MA. (Not yet implemented)
        """
        warnings.warn("MCMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_leoma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Leo MA. (Not yet implemented)
        """
        warnings.warn("LEOMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_cma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Corrective MA. (Not yet implemented)
        """
        warnings.warn("CMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_corma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Correlation MA. (Not yet implemented)
        """
        warnings.warn("CORMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_autol(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Auto-Line. (Not yet implemented)
        """
        warnings.warn("AUTOL is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_median(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Median MA. (Not yet implemented)
        """
        warnings.warn("MEDIAN is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_gma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Geometric MA. (Not yet implemented)
        """
        warnings.warn("GMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_xema(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Optimized Exponential MA. (Not yet implemented)
        """
        warnings.warn("XEMA is not yet implemented.")
        return np.full(len(source), np.nan)

    def calculate_zsma(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Zero-Lag Simple MA. (Not yet implemented)
        """
        warnings.warn("ZSMA is not yet implemented.")
        return np.full(len(source), np.nan)
    
    def calculate_ma(
        self,
        source: NumericList,
        method: str = "Simple",
        period: int = 14
    ) -> np.ndarray:
        """
        Calculate Moving Average using specified method.
        
        Args:
            source: Price series
            method: MA calculation method
            period: Calculation period
            
        Returns:
            MA values as numpy array
        """
        cache_key = f'{method}_{period}_{hash(tuple(source) if isinstance(source, list) else source.tobytes())}'
        
        if cache_key in self.ma_cache:
            return self.ma_cache[cache_key]
        
        method_upper = method.upper()
        
        if method_upper in ["SIMPLE", "SMA"]:
            result = self.calculate_sma(source, period)
        elif method_upper in ["EXPONENTIAL", "EXP", "EMA"]:
            result = self.calculate_ema(source, period)
        elif method_upper in ["WEIGHTED", "WMA"]:
            result = self.calculate_wma(source, period)
        elif method_upper in ["HULL", "HULLMA"]:
            result = self.calculate_hull_ma(source, period)
        elif method_upper == "TRIANGULAR":
            # Triangular MA is SMA of SMA
            sma_period = (period + 1) // 2
            sma1 = self.calculate_sma(source, sma_period)
            result = self.calculate_sma(sma1, sma_period)
        elif method_upper in ["WILDER", "RMA", "SMMA"]:
            # Wilder's smoothing (similar to EMA with different alpha)
            alpha = 1.0 / period
            result = self._calculate_wilder_ma(source, alpha)
        elif method_upper == "DEMA":
            result = self.calculate_dema(source, period)
        elif method_upper == "TEMA":
            result = self.calculate_tema(source, period)
        elif method_upper == "VWMA":
            result = self.calculate_vwma(source, period)
        elif method_upper == "LSMA":
            result = self.calculate_lsma(source, period)
        elif method_upper == "KAMA":
            result = self.calculate_kama(source, period)
        elif method_upper == "VIDYA":
            result = self.calculate_vidya(source, period)
        elif method_upper == "ZLEMA":
            result = self.calculate_zlema(source, period)
        elif method_upper == "T3":
            result = self.calculate_t3(source, period)
        elif method_upper == "ALMA":
            result = self.calculate_alma(source, period)
        elif method_upper == "JMA":
            result = self.calculate_jma(source, period)
        elif method_upper == "FRAMA":
            result = self.calculate_frama(source, period)
        elif method_upper == "MAMA":
            result = self.calculate_mama(source)
        elif method_upper == "MCGINLEY":
            result = self.calculate_mcginley(source, period)
        elif method_upper == "VAMA":
            result = self.calculate_vama(source, period)
        elif method_upper == "COVWMA":
            result = self.calculate_covwma(source, period)
        elif method_upper == "COVWEMA":
            result = self.calculate_covwema(source, period)
        elif method_upper == "FAMA":
            result = self.calculate_fama(source, period)
        elif method_upper == "SRWMA":
            result = self.calculate_srwma(source, period)
        elif method_upper == "SWMA":
            result = self.calculate_swma(source, period)
        elif method_upper == "EVWMA":
            result = self.calculate_evwma(source, period)
        elif method_upper == "REGMA":
            result = self.calculate_regma(source, period)
        elif method_upper == "REMA":
            result = self.calculate_rema(source, period)
        elif method_upper == "REPMA":
            result = self.calculate_repma(source, period)
        elif method_upper == "RSIMA":
            result = self.calculate_rsima(source, period)
        elif method_upper == "ETMA":
            result = self.calculate_etma(source, period)
        elif method_upper == "TREMA":
            result = self.calculate_trema(source, period)
        elif method_upper == "TRSMA":
            result = self.calculate_trsma(source, period)
        elif method_upper == "THMA":
            result = self.calculate_thma(source, period)
        elif method_upper == "DWMA":
            result = self.calculate_dwma(source, period)
        elif method_upper == "TWMA":
            result = self.calculate_twma(source, period)
        elif method_upper == "DVWMA":
            result = self.calculate_dvwma(source, period)
        elif method_upper == "TVWMA":
            result = self.calculate_tvwma(source, period)
        elif method_upper == "DHULL":
            result = self.calculate_dhull(source, period)
        elif method_upper == "THULL":
            result = self.calculate_thull(source, period)
        elif method_upper == "DZLEMA":
            result = self.calculate_dzlema(source, period)
        elif method_upper == "TZLEMA":
            result = self.calculate_tzlema(source, period)
        elif method_upper == "DSSMA":
            result = self.calculate_dssma(source, period)
        elif method_upper == "TSSMA":
            result = self.calculate_tssma(source, period)
        elif method_upper == "DSMMA":
            result = self.calculate_dsmma(source, period)
        elif method_upper == "TSMMA":
            result = self.calculate_tsmma(source, period)
        elif method_upper == "DSMA":
            result = self.calculate_dsma(source, period)
        elif method_upper == "TSMA":
            result = self.calculate_tsma(source, period)
        elif method_upper == "ADEMA":
            result = self.calculate_adema(source, period)
        elif method_upper == "EDMA":
            result = self.calculate_edma(source, period)
        elif method_upper == "EDSMA":
            result = self.calculate_edsma(source, period)
        elif method_upper == "AHMA":
            result = self.calculate_ahma(source, period)
        elif method_upper == "EHMA":
            result = self.calculate_ehma(source, period)
        elif method_upper == "ALSMA":
            result = self.calculate_alsma(source, period)
        elif method_upper == "AARMA":
            result = self.calculate_aarma(source, period)
        elif method_upper == "MCMA":
            result = self.calculate_mcma(source, period)
        elif method_upper == "LEOMA":
            result = self.calculate_leoma(source, period)
        elif method_upper == "CMA":
            result = self.calculate_cma(source, period)
        elif method_upper == "CORMA":
            result = self.calculate_corma(source, period)
        elif method_upper == "AUTOL":
            result = self.calculate_autol(source, period)
        elif method_upper == "MEDIAN":
            result = self.calculate_median(source, period)
        elif method_upper == "GMA":
            result = self.calculate_gma(source, period)
        elif method_upper == "XEMA":
            result = self.calculate_xema(source, period)
        elif method_upper == "ZSMA":
            result = self.calculate_zsma(source, period)
        else:
            # Default to Simple MA for unknown methods
            warnings.warn(f"Unknown MA method '{method}', using Simple MA")
            result = self.calculate_sma(source, period)
        
        # Cache result if cache is not full
        if len(self.ma_cache) < self.config.cache_size:
            self.ma_cache[cache_key] = result
        
        return result
    
    def _calculate_wilder_ma(self, source: NumericList, alpha: float) -> np.ndarray:
        """Calculate Wilder's smoothing (internal helper)."""
        source_array = np.array(source, dtype=np.float64)
        if len(source_array) == 0:
            return np.array([])
        
        # For RSI, start with simple average of first period
        period = int(1.0 / alpha)
        result = np.zeros_like(source_array)
        
        # Calculate initial average
        if len(source_array) >= period:
            result[period-1] = np.mean(source_array[:period])
            
            # Apply Wilder smoothing from period onwards
            for i in range(period, len(source_array)):
                result[i] = alpha * source_array[i] + (1 - alpha) * result[i-1]
        else:
            # Not enough data, just use simple average where possible
            for i in range(len(source_array)):
                if i == 0:
                    result[i] = source_array[i]
                else:
                    result[i] = alpha * source_array[i] + (1 - alpha) * result[i-1]
        
        return result
    
    # ==================== RSI Calculation ====================
    
    def calculate_rsi(self, source: NumericList, period: int = 14) -> np.ndarray:
        """
        Calculate Relative Strength Index.
        
        Args:
            source: Price series
            period: Calculation period
            
        Returns:
            RSI values as numpy array
        """
        if period <= 0:
            raise ValueError("Period must be positive")
        
        cache_key = f"RSI_{period}_{hash(tuple(source) if isinstance(source, list) else source.tobytes())}"
        
        if cache_key in self.rsi_cache:
            return self.rsi_cache[cache_key]
        
        source_array = np.array(source, dtype=np.float64)
        if len(source_array) < period + 1:
            return np.full(len(source_array), np.nan)
        
        # Calculate price differences
        diff = np.diff(source_array)
        
        # Separate gains and losses
        gains = np.where(diff > 0, diff, 0)
        losses = np.where(diff < 0, -diff, 0)
        
        # Initialize result array
        result = np.full(len(source_array), np.nan)
        
        # Calculate initial averages (SMA for first period)
        if len(gains) >= period:
            avg_gain = np.mean(gains[:period])
            avg_loss = np.mean(losses[:period])
            
            # Calculate RSI for the period+1 index (period gains/losses + 1 for original index)
            if avg_loss != 0:
                rs = avg_gain / avg_loss
                result[period] = 100 - (100 / (1 + rs))
            else:
                result[period] = 100.0 if avg_gain > 0 else 50.0
            
            # Continue with Wilder's smoothing
            alpha = 1.0 / period
            for i in range(period + 1, len(source_array)):
                gain_idx = i - 1  # diff index is one less than source index
                loss_idx = i - 1
                
                # Wilder's smoothing
                avg_gain = alpha * gains[gain_idx] + (1 - alpha) * avg_gain
                avg_loss = alpha * losses[loss_idx] + (1 - alpha) * avg_loss
                
                # Calculate RSI
                if avg_loss != 0:
                    rs = avg_gain / avg_loss
                    result[i] = 100 - (100 / (1 + rs))
                else:
                    result[i] = 100.0 if avg_gain > 0 else 50.0
        
        # Cache result
        if len(self.rsi_cache) < self.config.cache_size:
            self.rsi_cache[cache_key] = result
        
        return result
    
    # ==================== MACD Calculation ====================
    
    def calculate_macd(
        self,
        source: NumericList,
        fast_period: int = 12,
        slow_period: int = 26,
        signal_period: int = 9
    ) -> Tuple[np.ndarray, np.ndarray, np.ndarray]:
        """
        Calculate MACD (Moving Average Convergence Divergence).
        
        Args:
            source: Price series
            fast_period: Fast EMA period
            slow_period: Slow EMA period
            signal_period: Signal line EMA period
            
        Returns:
            Tuple of (MACD line, Signal line, Histogram)
        """
        cache_key = f"MACD_{fast_period}_{slow_period}_{signal_period}_{hash(tuple(source) if isinstance(source, list) else source.tobytes())}"
        
        if cache_key in self.macd_cache:
            return self.macd_cache[cache_key]
        
        if fast_period >= slow_period:
            raise ValueError("Fast period must be less than slow period")
        
        # Calculate fast and slow EMAs
        fast_ema = self.calculate_ema(source, fast_period)
        slow_ema = self.calculate_ema(source, slow_period)
        
        # MACD line = Fast EMA - Slow EMA
        macd_line = fast_ema - slow_ema
        
        # Signal line = EMA of MACD line
        signal_line = self.calculate_ema(macd_line, signal_period)
        
        # Histogram = MACD line - Signal line
        histogram = macd_line - signal_line
        
        result = (macd_line, signal_line, histogram)
        
        # Cache result
        if len(self.macd_cache) < self.config.cache_size:
            self.macd_cache[cache_key] = result
        
        return result
    
    # ==================== Bulk Operations ====================
    
    def fill_ma_list(
        self,
        source: NumericList,
        methods: Union[str, List[str]],
        periods: List[int]
    ) -> List[np.ndarray]:
        """
        Calculate multiple Moving Averages in bulk.
        
        Args:
            source: Price series
            methods: MA method(s) to calculate
            periods: List of periods to calculate
            
        Returns:
            List of MA arrays
        """
        if isinstance(methods, str):
            methods = [methods]
        
        self.ma_list.clear()
        self.ma_params_list.clear()
        
        for method in methods:
            for period in periods:
                param = f'{method}_{period}'
                self.ma_params_list.append(param)
                
                ma = self.calculate_ma(source, method, period)
                self.ma_list.append(ma)
                self.ma_dictionary[str(period)] = ma
        
        return self.ma_list.copy()
    
    def fill_rsi_list(
        self,
        source: NumericList,
        periods: List[int]
    ) -> List[np.ndarray]:
        """
        Calculate multiple RSI values in bulk.
        
        Args:
            source: Price series
            periods: List of periods to calculate
            
        Returns:
            List of RSI arrays
        """
        self.rsi_list.clear()
        self.rsi_params_list.clear()
        
        for period in periods:
            param = str(period)
            self.rsi_params_list.append(param)
            
            rsi = self.calculate_rsi(source, period)
            self.rsi_list.append(rsi)
        
        return self.rsi_list.copy()
    
    def fill_macd_list(
        self,
        source: NumericList,
        fast_periods: List[int],
        slow_periods: List[int],
        signal_period: int = 9
    ) -> List[Tuple[np.ndarray, np.ndarray, np.ndarray]]:
        """
        Calculate multiple MACD values in bulk.
        
        Args:
            source: Price series
            fast_periods: List of fast EMA periods
            slow_periods: List of slow EMA periods
            signal_period: Signal line period
            
        Returns:
            List of MACD tuples (line, signal, histogram)
        """
        self.macd_list.clear()
        self.macd_params_list.clear()
        
        for fast in fast_periods:
            for slow in slow_periods:
                if fast < slow:
                    param = f'{fast}_{slow}_{signal_period}'
                    self.macd_params_list.append(param)
                    
                    macd = self.calculate_macd(source, fast, slow, signal_period)
                    self.macd_list.append(macd)
        
        return self.macd_list.copy()
    
    # ==================== Utility Methods ====================
    
    def get_cache_stats(self) -> Dict[str, int]:
        """Get cache statistics for monitoring."""
        return {
            'ma_cache_size': len(self.ma_cache),
            'rsi_cache_size': len(self.rsi_cache),
            'macd_cache_size': len(self.macd_cache),
            'max_cache_size': self.config.cache_size
        }
    
    def clear_cache(self) -> None:
        """Clear all caches to free memory."""
        self.ma_cache.clear()
        self.rsi_cache.clear()
        self.macd_cache.clear()
    
    def __repr__(self) -> str:
        """String representation of the indicator manager."""
        return f"CIndicatorManager(bars={self.bar_count}, cache_stats={self.get_cache_stats()})"

    def calculate_most(self, period=21, percent=1.0):
        """
        Calculates the MOST (Moving Stop Loss) indicator based on the provided Pine Script logic.

        Args:
            period: The period for the Exponential Moving Average (EMA).
            percent: The percentage for the bands.

        Returns:
            A tuple containing two numpy arrays: (most, exmov)
        """
        if self.BarCount == 0:
            return np.array([]), np.array([])

        close_series = pd.Series(self.Close)
        exmov = close_series.ewm(span=period, adjust=False).mean().to_numpy()

        fark = exmov * (percent / 100.0)
        up = exmov - fark
        down = exmov + fark

        trend_up = np.zeros(self.BarCount)
        trend_down = np.zeros(self.BarCount)
        trend = np.zeros(self.BarCount)
        most = np.zeros(self.BarCount)

        # Initialize first values
        trend_up[0] = up[0]
        trend_down[0] = down[0]
        trend[0] = 1
        most[0] = trend_up[0]


        for i in range(1, self.BarCount):
            # Pine Script: TrendUp = prev(ExMov,1)>prev(TrendUp,1) ? max(Up,prev(TrendUp,1)) : Up
            if exmov[i-1] > trend_up[i-1]:
                trend_up[i] = max(up[i], trend_up[i-1])
            else:
                trend_up[i] = up[i]

            # Pine Script: TrendDown = prev(ExMov,1)<prev(TrendDown,1) ? min(Down,prev(TrendDown,1)) : Down
            if exmov[i-1] < trend_down[i-1]:
                trend_down[i] = min(down[i], trend_down[i-1])
            else:
                trend_down[i] = down[i]

            # Pine Script: Trend = ExMov>prev(TrendDown,1) ? 1 : ExMov<prev(TrendUp,1) ? -1 : prev(Trend,1)
            if exmov[i] > trend_down[i-1]:
                trend[i] = 1
            elif exmov[i] < trend_up[i-1]:
                trend[i] = -1
            else:
                trend[i] = trend[i-1]

            # Pine Script: MOST = Trend==1 ? TrendUp : TrendDown
            if trend[i] == 1:
                most[i] = trend_up[i]
            else:
                most[i] = trend_down[i]

        return most, exmov

    def calculate_supertrend(self, period: int = 10, multiplier: float = 3.0) -> Tuple[np.ndarray, np.ndarray]:
        """
        Calculate Supertrend indicator.
        
        Args:
            period: ATR period.
            multiplier: ATR multiplier.
            
        Returns:
            A tuple containing two numpy arrays: (supertrend, direction)
        """
        high_series = pd.Series(self.High)
        low_series = pd.Series(self.Low)
        close_series = pd.Series(self.Close)
        
        # Calculate True Range
        tr1 = pd.DataFrame(high_series - low_series)
        tr2 = pd.DataFrame(abs(high_series - close_series.shift(1)))
        tr3 = pd.DataFrame(abs(low_series - close_series.shift(1)))
        tr = pd.concat([tr1, tr2, tr3], axis=1, join='inner').max(axis=1)
        
        # Calculate ATR
        atr = tr.ewm(alpha=1/period, adjust=False).mean()
        
        # Basic Upper and Lower Bands
        upper_band = (high_series + low_series) / 2 + (multiplier * atr)
        lower_band = (high_series + low_series) / 2 - (multiplier * atr)
        
        # Final Upper and Lower Bands
        final_upper_band = upper_band.copy()
        final_lower_band = lower_band.copy()
        
        for i in range(1, len(close_series)):
            if close_series[i-1] <= final_upper_band[i-1]:
                final_upper_band[i] = min(upper_band[i], final_upper_band[i-1])
            else:
                final_upper_band[i] = upper_band[i]
                
            if close_series[i-1] >= final_lower_band[i-1]:
                final_lower_band[i] = max(lower_band[i], final_lower_band[i-1])
            else:
                final_lower_band[i] = lower_band[i]

        # Supertrend
        supertrend = np.full(len(close_series), np.nan)
        direction = np.full(len(close_series), 1)

        for i in range(1, len(close_series)):
            if close_series[i] > final_upper_band[i-1]:
                direction[i] = 1
            elif close_series[i] < final_lower_band[i-1]:
                direction[i] = -1
            else:
                direction[i] = direction[i-1]
                if direction[i] == 1 and final_lower_band[i] > final_lower_band[i-1]:
                    final_lower_band[i] = final_lower_band[i-1]
                if direction[i] == -1 and final_upper_band[i] < final_upper_band[i-1]:
                    final_upper_band[i] = final_upper_band[i-1]

            if direction[i] == 1:
                supertrend[i] = final_lower_band[i]
            else:
                supertrend[i] = final_upper_band[i]

        return supertrend, direction

    def calculate_hhv(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Highest High Value (HHV).

        Calculates the highest value over the preceding periods (includes current time).

        Args:
            source: Price series (e.g., close, high, low)
            period: Lookback period

        Returns:
            HHV values as numpy array
        """
        if period <= 0:
            raise ValueError("Period must be positive")

        source_array = np.array(source, dtype=np.float64)
        if len(source_array) < period:
            return np.full(len(source_array), np.nan)

        result = np.full(len(source_array), np.nan)

        # Use pandas rolling max for efficient calculation
        series = pd.Series(source_array)
        hhv = series.rolling(window=period, min_periods=period).max()

        return hhv.values

    def calculate_llv(self, source: NumericList, period: int) -> np.ndarray:
        """
        Calculate Lowest Low Value (LLV).

        Calculates the lowest value over the preceding periods (includes current time).

        Args:
            source: Price series (e.g., close, high, low)
            period: Lookback period

        Returns:
            LLV values as numpy array
        """
        if period <= 0:
            raise ValueError("Period must be positive")

        source_array = np.array(source, dtype=np.float64)
        if len(source_array) < period:
            return np.full(len(source_array), np.nan)

        result = np.full(len(source_array), np.nan)

        # Use pandas rolling min for efficient calculation
        series = pd.Series(source_array)
        llv = series.rolling(window=period, min_periods=period).min()

        return llv.values
