// Day timeline for one room: click the first slot and then the last one, and a link to the
// booking page is filled in. Which slots are taken comes from /rooms/{id}/day.
import { buildSlots, pickRange, rangeTimes, hoursBetween } from "./timeline-model.js";

const MAX_SLOTS = 16; // 8 hours of 30 minutes, same as the server rule

function h(tag, attrs, ...children) {
  const el = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs ?? {})) {
    if (v === null || v === undefined || v === false) continue;
    if (k === "class") el.className = v;
    else if (k.startsWith("on") && typeof v === "function") el.addEventListener(k.slice(2), v);
    else el.setAttribute(k, v === true ? "" : String(v));
  }
  for (const c of children.flat()) if (c !== null && c !== undefined && c !== false) el.append(c instanceof Node ? c : document.createTextNode(String(c)));
  return el;
}

const money = new Intl.NumberFormat("en-GB", { style: "currency", currency: "GBP" });

async function start(root) {
  const { url, date, bookingUrl, room } = root.dataset;
  const capacity = Number(root.dataset.capacity);
  const price = Number(root.dataset.price);

  let day;
  try {
    const response = await fetch(`${url}?date=${encodeURIComponent(date)}`, { headers: { Accept: "application/json" } });
    if (!response.ok) throw new Error();
    day = await response.json();
  } catch {
    root.replaceChildren(h("p", { class: "form-error" }, "The timeline could not be loaded."));
    return;
  }

  const slots = buildSlots(day.open, day.close, day.slotMinutes, day.busy);
  const now = new Date();
  // slots that are already in the past can't be picked
  for (const s of slots) {
    const at = new Date(`${date}T${String(Math.floor(s.start / 60)).padStart(2, "0")}:${String(s.start % 60).padStart(2, "0")}:00`);
    if (at <= now) s.busy = true;
  }

  let selection = null;   // { from, to } as slot indexes
  let picking = false;    // true after the first click, until the second one

  const people = h("input", { type: "number", min: 1, max: capacity, value: Math.min(2, capacity), "aria-label": "Number of people" });
  const summary = h("div", { class: "timeline-summary", "aria-live": "polite" });
  const grid = h("div", { class: "slots", role: "group", "aria-label": "Time slots" });

  function click(i) {
    if (slots[i].busy) return;
    if (!picking) {
      selection = { from: i, to: i };
      picking = true;
    } else {
      const range = pickRange(slots, selection.from, i, MAX_SLOTS);
      if (range) { selection = range; picking = false; }
      else selection = { from: i, to: i }; // something is in the way: start again from here
    }
    draw();
  }

  function draw() {
    grid.replaceChildren(...slots.map((s, i) => {
      const chosen = selection && i >= selection.from && i <= selection.to;
      const label = `${rangeTimes(slots, { from: i, to: i }).start}`;
      return h("button", {
        type: "button",
        class: "slot" + (s.busy ? " slot-busy" : "") + (chosen ? " slot-chosen" : ""),
        disabled: s.busy,
        "aria-pressed": chosen ? "true" : "false",
        "aria-label": `${label}${s.busy ? ", not available" : ""}`,
        onclick: () => click(i),
      }, s.start % 60 === 0 ? label : " ");
    }));

    if (!selection) {
      summary.replaceChildren(h("p", { class: "muted" }, "Pick a time to see the price."));
      return;
    }
    const { start, end } = rangeTimes(slots, selection);
    const hours = hoursBetween(start, end);
    const href = `${bookingUrl}?roomId=${encodeURIComponent(room)}&start=${date}T${start}&end=${date}T${end}&people=${encodeURIComponent(people.value || 1)}`;
    summary.replaceChildren(
      h("p", {}, h("strong", {}, `${start} to ${end}`), `  (${hours} h, ${money.format(hours * price)})`),
      h("label", { class: "check" }, "People ", people),
      h("a", { class: "btn", href }, "Book this time"));
  }

  people.addEventListener("input", () => { if (selection) draw(); });

  root.replaceChildren(
    h("div", { class: "slot-legend" }, h("span", { class: "slot slot-free" }, "free"), h("span", { class: "slot slot-busy" }, "taken"), h("span", { class: "slot slot-chosen" }, "yours")),
    grid, summary);
  draw();
}

const root = document.getElementById("timeline");
if (root) start(root);
