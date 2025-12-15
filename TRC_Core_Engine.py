// One TRCEngine object per symbol/timeframe
// Feed it bars + HTF bias each step
// Maintains internal state (trend, lastHL/LH, CHoCH, retest zone)
// Returns events (BOS, CHOCH, TRC long/short entries) without any charting glue

// Unit-testable logic
// Integratable into Python backtester / Streamlit UI, MT5/cTrader bridge
// (Pine “view” that just mirrors the same transitions)

from dataclasses import dataclass
from enum import Enum, auto
from typing import Optional, List


class Trend(Enum):
    NONE = 0
    BULL = 1
    BEAR = -1


class EventType(Enum):
    NONE = 0
    BOS_UP = auto()
    BOS_DOWN = auto()
    CHOCH_UP = auto()
    CHOCH_DOWN = auto()
    TRC_LONG_ENTRY = auto()
    TRC_SHORT_ENTRY = auto()


@dataclass
class Bar:
    time: any        # datetime or int index
    open: float
    high: float
    low: float
    close: float
    volume: float = 0.0


@dataclass
class Event:
    time: any
    type: EventType
    info: dict


@dataclass
class TRCConfig:
    max_retest_bars: int = 20  # how long after CHOCH we accept retests


@dataclass
class TRCState:
    # 5m structure state
    trend: Trend = Trend.NONE
    last_high: Optional[float] = None
    last_low: Optional[float] = None
    last_hl: Optional[float] = None   # last Higher Low (bull)
    last_lh: Optional[float] = None   # last Lower High (bear)

    # retest setup state
    setup_dir: int = 0                # 1 = long, -1 = short, 0 = none
    zone_low: Optional[float] = None
    zone_high: Optional[float] = None
    bars_left: int = 0
    setup_bar_index: Optional[int] = None


class TRCEngine:
    """
    Canonical TRC engine: 5m structure, CHOCH, retest.
    HTF bias is provided per bar as +1 (bull), -1 (bear), 0 (neutral).
    """

    def __init__(self, config: Optional[TRCConfig] = None):
        self.cfg = config or TRCConfig()
        self.state = TRCState()
        self._bar_index = 0

    # ---------------------------
    # public API
    # ---------------------------

    def on_bar(self, bar: Bar, htf_bias: int) -> List[Event]:
        """
        Process one new bar and return any events fired on this bar.
        htf_bias: +1 (bull), -1 (bear), 0 (neutral).
        """
        self._bar_index += 1
        events: List[Event] = []

        # Initialize structure on very first bar
        if self._bar_index == 1:
            self.state.last_high = bar.high
            self.state.last_low = bar.low
            self.state.trend = Trend.BULL if bar.close > bar.open else Trend.BEAR
            return events

        # 1) Update structure (BOS / CHOCH)
        struct_event = self._update_structure(bar)
        if struct_event is not None:
            events.append(struct_event)

            # 2) Create new retest setup when CHOCH aligns with HTF bias
            if struct_event.type == EventType.CHOCH_UP and htf_bias == 1:
                self._create_retest_setup(bar, direction=1)
            elif struct_event.type == EventType.CHOCH_DOWN and htf_bias == -1:
                self._create_retest_setup(bar, direction=-1)

        # 3) Handle retest logic (TRC entry)
        trc_event = self._check_retest(bar)
        if trc_event is not None:
            events.append(trc_event)

        return events

    # ---------------------------
    # internal structure logic
    # ---------------------------

    def _update_structure(self, bar: Bar) -> Optional[Event]:
        s = self.state
        event: Optional[Event] = None

        # bootstrap trend if NONE
        if s.trend == Trend.NONE:
            s.trend = Trend.BULL if bar.close > bar.open else Trend.BEAR

        # BULL trend logic
        if s.trend == Trend.BULL:
            # CHOCH DOWN: close below last HL
            if s.last_hl is not None and bar.close < s.last_hl:
                s.trend = Trend.BEAR
                s.last_lh = bar.high
                s.last_high = bar.high
                s.last_low = bar.low
                event = Event(bar.time, EventType.CHOCH_DOWN, {"level": s.last_hl})

            else:
                # BOS UP: new impulse high
                if s.last_high is None or bar.high > s.last_high:
                    s.last_high = bar.high
                    if s.last_low is not None:
                        s.last_hl = s.last_low
                    event = Event(bar.time, EventType.BOS_UP, {"level": s.last_high})

                # track lowest low since last high
                if s.last_low is None or bar.low < s.last_low:
                    s.last_low = bar.low

        # BEAR trend logic
        elif s.trend == Trend.BEAR:
            # CHOCH UP: close above last LH
            if s.last_lh is not None and bar.close > s.last_lh:
                s.trend = Trend.BULL
                s.last_hl = bar.low
                s.last_low = bar.low
                s.last_high = bar.high
                event = Event(bar.time, EventType.CHOCH_UP, {"level": s.last_lh})

            else:
                # BOS DOWN: new impulse low
                if s.last_low is None or bar.low < s.last_low:
                    s.last_low = bar.low
                    if s.last_high is not None:
                        s.last_lh = s.last_high
                    event = Event(bar.time, EventType.BOS_DOWN, {"level": s.last_low})

                # track highest high since last low
                if s.last_high is None or bar.high > s.last_high:
                    s.last_high = bar.high

        return event

    # ---------------------------
    # retest (C-leg) logic
    # ---------------------------

    def _create_retest_setup(self, bar: Bar, direction: int) -> None:
        """
        direction: +1 (long), -1 (short)
        """
        s = self.state
        s.setup_dir = direction
        s.bars_left = self.cfg.max_retest_bars
        s.setup_bar_index = self._bar_index

        if direction == 1:
            # bullish CHOCH: retest zone ~ [low, close] of CHOCH bar
            s.zone_low = bar.low
            s.zone_high = bar.close
        else:
            # bearish CHOCH: retest zone ~ [close, high]
            s.zone_low = bar.close
            s.zone_high = bar.high

    def _check_retest(self, bar: Bar) -> Optional[Event]:
        s = self.state
        if s.setup_dir == 0 or s.bars_left <= 0:
            return None

        s.bars_left -= 1

        # Long setup: price retests between [zone_low, zone_high] with low
        if s.setup_dir == 1 and self._bar_index > (s.setup_bar_index or 0):
            if bar.low <= s.zone_high and bar.low >= s.zone_low:
                # Trigger TRC long entry
                evt = Event(bar.time, EventType.TRC_LONG_ENTRY, {
                    "entry_zone": (s.zone_low, s.zone_high)
                })
                self._clear_setup()
                return evt

        # Short setup: retest with high
        if s.setup_dir == -1 and self._bar_index > (s.setup_bar_index or 0):
            if bar.high >= s.zone_low and bar.high <= s.zone_high:
                evt = Event(bar.time, EventType.TRC_SHORT_ENTRY, {
                    "entry_zone": (s.zone_low, s.zone_high)
                })
                self._clear_setup()
                return evt

        # Expire if no bars left
        if s.bars_left <= 0:
            self._clear_setup()

        return None

    def _clear_setup(self) -> None:
        s = self.state
        s.setup_dir = 0
        s.zone_low = None
        s.zone_high = None
        s.bars_left = 0
        s.setup_bar_index = None
