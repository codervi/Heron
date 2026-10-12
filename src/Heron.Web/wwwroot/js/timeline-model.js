// The logic behind the day timeline, kept apart from the page so it can be tested.
// Times are minutes since midnight inside this file, and "09:30" text at the edges.

const pad = (n) => String(n).padStart(2, "0");

export const toMinutes = (hhmm) => {
  const [h, m] = hhmm.split(":").map(Number);
  return h * 60 + m;
};

export const toText = (minutes) => `${pad(Math.floor(minutes / 60))}:${pad(minutes % 60)}`;

// One entry per slot between opening and closing. A slot is busy when a booking covers it.
// Bookings are half open ranges: one that ends at 10:00 does not block the slot starting at 10:00.
export function buildSlots(open, close, step, busy) {
  const ranges = busy.map((b) => [toMinutes(b.start), toMinutes(b.end)]);
  const slots = [];
  for (let start = toMinutes(open); start + step <= toMinutes(close); start += step) {
    const end = start + step;
    slots.push({ start, end, busy: ranges.some(([s, e]) => s < end && e > start) });
  }
  return slots;
}

// Slots from index a to index b (either order) as one range, or null when one of them is busy
// or the range is longer than maxSlots.
export function pickRange(slots, a, b, maxSlots) {
  const from = Math.min(a, b);
  const to = Math.max(a, b);
  if (to - from + 1 > maxSlots) return null;
  for (let i = from; i <= to; i++) if (slots[i].busy) return null;
  return { from, to };
}

export function rangeTimes(slots, range) {
  return { start: toText(slots[range.from].start), end: toText(slots[range.to].end) };
}

export const hoursBetween = (start, end) => (toMinutes(end) - toMinutes(start)) / 60;
