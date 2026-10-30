const $ = id => document.getElementById(id);

let study = null;
let state = null;

function send(message) {
    window.parent.postMessage(message, "*");
}

function notify(method, params) {
    send({ jsonrpc: "2.0", method, params });
}

let nextId = 1;
const pending = new Map();

function request(method, params) {
    const id = nextId++;
    send({ jsonrpc: "2.0", id, method, params });
    return new Promise((resolve, reject) => pending.set(id, { resolve, reject }));
}

window.addEventListener("message", event => {
    const message = event.data;
    if (!message || message.jsonrpc !== "2.0") return;

    if (message.id !== undefined && !message.method) {
        const call = pending.get(message.id);
        pending.delete(message.id);
        if (call) message.error ? call.reject(message.error) : call.resolve(message.result);
        return;
    }

    if (message.id !== undefined) send({ jsonrpc: "2.0", id: message.id, result: {} });

    if (message.method === "ui/notifications/tool-result") onFirstStudy(message.params);
    if (message.method === "ui/notifications/host-context-changed") applyTheme(message.params);
});

function applyTheme(context) {
    if (!context) return;
    for (const [name, value] of Object.entries(context.styles?.variables ?? {})) {
        if (value) document.documentElement.style.setProperty(name, value);
    }
    if (context.theme) document.documentElement.style.colorScheme = context.theme;
}

function sendSize() {
    const html = document.documentElement;
    const previous = html.style.height;
    html.style.height = "max-content";
    const height = Math.ceil(html.getBoundingClientRect().height);
    html.style.height = previous;
    notify("ui/notifications/size-changed", { width: Math.ceil(window.innerWidth), height });
}

async function start() {
    const result = await request("ui/initialize", {
        protocolVersion: "2026-01-26",
        appInfo: { name: "trade-off", version: "0.1.0" },
        appCapabilities: {}
    });
    applyTheme(result?.hostContext);
    notify("ui/notifications/initialized", {});
}

function el(tag, className, text) {
    const element = document.createElement(tag);
    if (className) element.className = className;
    if (text !== undefined) element.textContent = text;
    return element;
}

const ICONS = {
    grip: '<circle cx="9" cy="6" r="1"/><circle cx="15" cy="6" r="1"/><circle cx="9" cy="12" r="1"/><circle cx="15" cy="12" r="1"/><circle cx="9" cy="18" r="1"/><circle cx="15" cy="18" r="1"/>',
    excluded: '<circle cx="12" cy="12" r="9"/><path d="m15 9-6 6M9 9l6 6"/>'
};

function icon(name, size) {
    const image = svg("svg", { class: "ic", width: size, height: size, viewBox: "0 0 24 24", fill: "none", stroke: "currentColor", "stroke-width": "1.75", "stroke-linecap": "round", "stroke-linejoin": "round" });
    image.innerHTML = ICONS[name];
    return image;
}

function formatNumber(number) {
    return String(Number(number.toPrecision(6)));
}

function signed(number) {
    return (number < 0 ? "−" : "+") + formatNumber(Math.abs(number));
}

function unitOf(attribute) {
    return study.attributes.find(a => a.name === attribute)?.unit;
}

function withUnit(value, attribute) {
    const unit = unitOf(attribute);
    return unit ? formatNumber(value) + " " + unit : formatNumber(value);
}

function valuesText(values) {
    return Object.entries(values).map(([attribute, value]) => withUnit(value, attribute)).join(" · ");
}

function describe(choices) {
    return choices.map(c => c.choice + ": " + c.variant + (c.quantity > 1 ? " × " + c.quantity : "")).join(" · ");
}

function titleOf(study) {
    return study.title ?? "Trade-off · " + study.choices.map(choice => choice.name).join(", ");
}

const STATUS_TEXT = {
    Optimal: "best options found",
    Feasible: "good options found, maybe not the best",
    Infeasible: "no combination meets the constraints",
    Unknown: "the solver stopped before finding an answer"
};

function onFirstStudy(result) {
    const first = result?.structuredContent;
    if (!first) {
        showError(result?.content?.[0]?.text ?? "The host sent no trade-off to show.");
        return;
    }
    state = {
        request: structuredClone(first.request),
        choices: first.choices,
        excluded: new Set(first.request.excluded ?? []),
        objectives: first.request.objectives.map(objective => ({ ...objective })),
        constraints: first.request.constraints.map(constraint => ({ ...constraint, enabled: true })),
        selected: 1,
        lastCall: 0
    };
    show(first);
    drawSetup(state);
}

function show(newStudy) {
    study = newStudy;
    $("error").hidden = true;
    drawResults(study);
}

function drawResults(study) {
    drawHeader(study);
    drawChart(study);
    drawOptions(study);
    drawExcluded(study);
    drawPick(study);
}

function drawHeader(study) {
    $("title").textContent = titleOf(study);
    const count = study.pointsComplete ? study.points.length + " combinations" : "too many combinations to show them all";
    $("subtitle").textContent = [study.root.qualifiedName, count, STATUS_TEXT[study.status] ?? study.status].join(" · ");
}

function drawOptions(study) {
    const target = $("options");
    target.replaceChildren();
    $("today").textContent = "vs today: " + study.current.totals.map(total => withUnit(total.total, total.attribute)).join(" · ");

    for (const option of study.best) {
        const card = el("div", option.rank === state.selected ? "tr-option on" : "tr-option");
        card.dataset.rank = option.rank;
        const body = el("div");
        body.append(el("div", "tr-name", describe(option.choices)), drawTotals(option.totals), drawChecks(option.constraints));
        card.append(el("span", "tr-rank n" + option.rank, option.rank), body);
        target.append(card);
    }
    if (study.best.length === 0) target.append(el("div", "tr-empty", "No combination meets the constraints."));
}

function drawTotals(totals) {
    const line = el("div", "tr-totals");
    totals.forEach((total, index) => {
        if (index > 0) line.append(" · ");
        line.append(withUnit(total.total, total.attribute) + " ", drawDifference(total));
    });
    return line;
}

function drawDifference(total) {
    if (total.difference === 0) return el("span", null, "(=)");
    const objective = study.objectives.find(o => o.attribute === total.attribute);
    const better = objective?.direction === "Maximize" ? total.difference > 0 : total.difference < 0;
    return el("span", better ? "tr-better" : "tr-worse", "(" + signed(total.difference) + ")");
}

function drawChecks(constraints) {
    const line = el("div", "tr-checks");
    for (const check of constraints) {
        let text = (check.satisfied ? "✓ " : "✗ ") + (check.name ?? check.constraint);
        if (check.value != null) text += " · " + formatNumber(check.value) + " / " + formatNumber(check.limit);
        line.append(el("span", check.satisfied ? "tagv ok" : "tagv bad", text));
    }
    return line;
}

function drawExcluded(study) {
    const target = $("excluded");
    target.replaceChildren();
    const rejected = study.points.filter(point => !point.feasible);
    if (rejected.length > 0) {
        target.append(el("h3", null, "Excluded (" + rejected.length + ")"));
        for (const point of rejected) target.append(drawRejected(describe(point.choices), point.broken.join(" · ")));
    }
    if (study.conflicts.length > 0) {
        target.append(el("h3", null, "Why no combination works"));
        for (const conflict of study.conflicts) target.append(drawRejected(null, conflict));
    }
}

function drawRejected(name, reason) {
    const text = el("span");
    if (name) text.append(el("b", null, name), " · ");
    text.append(reason);
    const line = el("div", "tr-rejected");
    line.append(icon("excluded", 13), text);
    return line;
}

const SVG_NS = "http://www.w3.org/2000/svg";
const BOX = { width: 560, height: 320, left: 56, right: 544, top: 16, bottom: 276 };

function svg(tag, attributes, text) {
    const element = document.createElementNS(SVG_NS, tag);
    for (const [name, value] of Object.entries(attributes)) element.setAttribute(name, value);
    if (text !== undefined) element.textContent = text;
    return element;
}

function makeScale(values, from, to) {
    let min = Math.min(...values);
    let max = Math.max(...values);
    const padding = (max - min) * 0.1 || 1;
    min -= padding;
    max += padding;
    return { min, max, at: value => from + (value - min) / (max - min) * (to - from) };
}

function niceStep(range) {
    const raw = range / 6;
    const power = 10 ** Math.floor(Math.log10(raw));
    return [1, 2, 5, 10].map(factor => factor * power).find(step => step >= raw);
}

function ticks(scale) {
    const step = niceStep(scale.max - scale.min);
    const values = [];
    for (let value = Math.ceil(scale.min / step) * step; value <= scale.max; value += step) values.push(value);
    return values;
}

function totalsByName(totals) {
    return Object.fromEntries(totals.map(total => [total.attribute, total.total]));
}

function axisName(attribute) {
    const unit = unitOf(attribute);
    return unit ? attribute + " [" + unit + "]" : attribute;
}

function drawChart(study) {
    const target = $("chart");
    target.replaceChildren();

    const names = [...new Set([...study.objectives.map(o => o.attribute), ...study.attributes.map(a => a.name)])];
    const [xName, yName] = names;
    if (!yName) {
        target.append(el("div", "tr-note", "One attribute only: see the list of best options."));
        $("legend").replaceChildren();
        return;
    }

    const today = totalsByName(study.current.totals);
    const xs = [today[xName], ...study.points.map(point => point.totals[xName])];
    const ys = [today[yName], ...study.points.map(point => point.totals[yName])];
    const limits = study.constraints.filter(c => c.threshold != null && (c.attribute === xName || c.attribute === yName));
    for (const limit of limits) (limit.attribute === xName ? xs : ys).push(limit.threshold);
    const x = makeScale(xs, BOX.left, BOX.right);
    const y = makeScale(ys, BOX.bottom, BOX.top);

    const chart = svg("svg", { viewBox: `0 0 ${BOX.width} ${BOX.height}`, class: "tr-svg" });

    for (const value of ticks(x)) {
        chart.append(svg("line", { x1: x.at(value), x2: x.at(value), y1: BOX.top, y2: BOX.bottom, class: "tr-gridline" }));
        chart.append(svg("text", { x: x.at(value), y: BOX.bottom + 16, "text-anchor": "middle" }, formatNumber(value)));
    }
    for (const value of ticks(y)) {
        chart.append(svg("line", { x1: BOX.left, x2: BOX.right, y1: y.at(value), y2: y.at(value), class: "tr-gridline" }));
        chart.append(svg("text", { x: BOX.left - 6, y: y.at(value) + 3, "text-anchor": "end" }, formatNumber(value)));
    }
    const middleY = (BOX.top + BOX.bottom) / 2;
    chart.append(svg("text", { x: (BOX.left + BOX.right) / 2, y: BOX.height - 8, "text-anchor": "middle", class: "tr-axis" }, axisName(xName)));
    chart.append(svg("text", { x: 14, y: middleY, "text-anchor": "middle", transform: `rotate(-90 14 ${middleY})`, class: "tr-axis" }, axisName(yName)));

    for (const limit of limits) drawLimit(chart, limit, limit.attribute === xName, limit.attribute === xName ? x : y);

    const todayX = x.at(today[xName]);
    const todayY = y.at(today[yName]);
    chart.append(svg("circle", { cx: todayX, cy: todayY, r: 12, class: "tr-today-dot" }));

    for (const point of study.points) chart.append(drawPoint(study, point, x.at(point.totals[xName]), y.at(point.totals[yName])));

    chart.append(svg("text", { x: todayX, y: todayY - 16, "text-anchor": "middle" }, "today"));
    target.append(chart);
    drawLegend(limits);
}

function drawLegend(limits) {
    const item = (className, text) => {
        const span = el("span");
        span.append(el("i", className), text);
        return span;
    };
    $("legend").replaceChildren(item("best", "Best options"), item("ok", "Meets the constraints"), item("bad", "Breaks a constraint"));
    for (const limit of limits) $("legend").append(item("limit", (limit.name ?? limit.attribute) + " limit"));
}

function drawLimit(chart, limit, onX, scale) {
    const at = scale.at(limit.threshold);
    const end = scale.at(limit.operator.startsWith("<") ? scale.max : scale.min);
    const from = Math.min(at, end);
    const size = Math.abs(end - at);
    const label = (limit.name ?? limit.description) + " · " + withUnit(limit.threshold, limit.attribute);
    if (onX) {
        chart.append(svg("rect", { x: from, y: BOX.top, width: size, height: BOX.bottom - BOX.top, class: "tr-forbidden" }));
        chart.append(svg("line", { x1: at, x2: at, y1: BOX.top, y2: BOX.bottom, class: "tr-limit" }));
        chart.append(svg("text", { x: at - 4, y: BOX.top + 12, "text-anchor": "end", class: "tr-limit-label" }, label));
    } else {
        chart.append(svg("rect", { x: BOX.left, y: from, width: BOX.right - BOX.left, height: size, class: "tr-forbidden" }));
        chart.append(svg("line", { x1: BOX.left, x2: BOX.right, y1: at, y2: at, class: "tr-limit" }));
        chart.append(svg("text", { x: BOX.right - 4, y: at - 4, "text-anchor": "end", class: "tr-limit-label" }, label));
    }
}

function drawPoint(study, point, cx, cy) {
    const isBest = point.rank != null && point.rank <= study.best.length;
    const group = svg("g", { class: !point.feasible ? "pt pt-bad" : isBest ? "pt pt-best pt-rank" + point.rank : "pt pt-ok" });
    group.append(svg("title", {}, describe(point.choices) + " · " + valuesText(point.totals)));
    if (isBest && point.rank === state.selected) group.append(svg("circle", { cx, cy, r: 13, class: "tr-ring" }));
    group.append(svg("circle", { cx, cy, r: 8 }));
    if (isBest) {
        group.dataset.rank = point.rank;
        group.append(svg("text", { x: cx, y: cy + 3.5, "text-anchor": "middle" }, point.rank));
    }
    if (!point.feasible) group.append(svg("path", { d: `M${cx - 3} ${cy - 3}l6 6M${cx + 3} ${cy - 3}l-6 6` }));
    return group;
}

function drawSetup(state) {
    drawChoices(state);
    drawObjectives(state);
    drawConstraints(state);
}

function drawChoices(state) {
    const target = $("choices");
    target.replaceChildren(el("h3", null, "Choices"));
    for (const choice of state.choices) {
        const heading = el("div", "tr-choice", choice.name);
        const owner = choice.qualifiedName?.split("::").at(-2);
        if (owner) heading.append(el("small", null, owner));
        target.append(heading);
        for (const variant of choice.variants) {
            const key = choice.name + "." + variant.name;
            const box = el("input", "tr-check");
            box.type = "checkbox";
            box.checked = !state.excluded.has(key);
            box.dataset.kind = "variant";
            box.dataset.key = key;
            const label = el("label", "tr-variant");
            label.append(box, el("span", null, variant.isCurrent ? "Current" : variant.name), el("span", "tr-values", valuesText(variant.values)));
            target.append(label);
        }
    }
}

function drawObjectives(state) {
    const list = el("div", state.objectives.length > 1 ? "tr-objectives" : "tr-objectives single");
    state.objectives.forEach((objective, index) => {
        const line = el("div", "tr-objective");
        const way = objective.direction === "Maximize" ? "highest first" : "lowest first";
        line.append(el("span", "tr-rank", index + 1), el("span", null, objective.attribute + ", " + way));
        if (state.objectives.length > 1) {
            line.tabIndex = 0;
            line.title = "Drag to change the order, or use the ↑ and ↓ keys";
            line.dataset.index = index;
            line.append(icon("grip", 14));
        }
        list.append(line);
    });
    $("objectives").replaceChildren(el("h3", null, "Objectives, in order"), list);
}

const OPERATORS = { "<=": "≤", ">=": "≥", "==": "=" };

function drawConstraints(state) {
    const target = $("constraints");
    target.replaceChildren(el("h3", null, "Constraints"));
    state.constraints.forEach((constraint, index) => {
        const toggle = el("input", "tr-switch");
        toggle.type = "checkbox";
        toggle.role = "switch";
        toggle.checked = constraint.enabled;
        toggle.dataset.kind = "enabled";
        toggle.dataset.key = index;
        const name = (constraint.name ?? "Constraint " + (index + 1)) + " · " + constraint.attribute + " " + (OPERATORS[constraint.operator] ?? constraint.operator);
        const label = el("label");
        label.append(toggle, el("span", null, name));

        const limits = el("div", "tr-limits");
        if (constraint.limitAttribute == null) {
            limits.append(numberInput("limit", index, constraint.limit), constraint.unit ?? unitOf(constraint.attribute) ?? "");
        } else {
            limits.append(constraint.limitAttribute);
        }
        limits.append(" · margin", numberInput("margin", index, constraint.margin ?? 0), "%");

        const line = el("div", "tr-constraint");
        line.append(label, limits);
        target.append(line);
    });
    if (state.constraints.length === 0) target.append(el("div", "tr-note", "No constraint."));
}

function numberInput(kind, index, value) {
    const input = el("input", "tr-number");
    input.type = "number";
    input.step = "any";
    input.value = value;
    input.dataset.kind = kind;
    input.dataset.key = index;
    input.ariaLabel = kind;
    return input;
}

function onSetupChange(event) {
    const input = event.target;
    const kind = input.dataset.kind;
    if (kind === "variant") {
        if (input.checked) state.excluded.delete(input.dataset.key);
        else state.excluded.add(input.dataset.key);
    } else if (kind === "enabled") {
        state.constraints[Number(input.dataset.key)].enabled = input.checked;
    } else if (kind === "limit" || kind === "margin") {
        if (input.value === "") return;
        state.constraints[Number(input.dataset.key)][kind] = Number(input.value);
    } else {
        return;
    }
    solve();
}

function moveObjective(from, to) {
    const [objective] = state.objectives.splice(from, 1);
    state.objectives.splice(to, 0, objective);
    drawObjectives(state);
    solve();
}

let drag = null;

function onObjectiveGrab(event) {
    const line = event.target.closest(".tr-objective[data-index]");
    if (!line || event.button !== 0) return;
    line.setPointerCapture(event.pointerId);
    line.classList.add("dragging");
    drag = { line, from: Number(line.dataset.index), offset: event.clientY - line.getBoundingClientRect().top };
}

function onObjectiveDrag(event) {
    if (!drag) return;
    const lines = [...drag.line.parentElement.children];
    const current = lines.indexOf(drag.line);
    const index = lines.filter(line => line !== drag.line && event.clientY > middleOf(line)).length;
    if (index < current) drag.line.after(...lines.slice(index, current));
    if (index > current) drag.line.before(...lines.slice(current + 1, index + 1));
    [...drag.line.parentElement.children].forEach((line, position) => line.querySelector(".tr-rank").textContent = position + 1);

    drag.line.style.transform = "";
    const top = drag.line.getBoundingClientRect().top;
    drag.line.style.transform = `translateY(${event.clientY - drag.offset - top}px)`;
}

function middleOf(element) {
    const box = element.getBoundingClientRect();
    return box.top + box.height / 2;
}

function onObjectiveDrop(event) {
    if (!drag) return;
    const { line, from } = drag;
    const to = [...line.parentElement.children].indexOf(line);
    drag = null;
    line.classList.remove("dragging");
    line.style.transform = "";
    if (to === from) return;
    if (event.type === "pointerup") moveObjective(from, to);
    else drawObjectives(state);
}

function onObjectiveKey(event) {
    const line = event.target.closest(".tr-objective[data-index]");
    const step = { ArrowUp: -1, ArrowDown: 1 }[event.key];
    if (!line || !step) return;
    const from = Number(line.dataset.index);
    const to = from + step;
    if (to < 0 || to >= state.objectives.length) return;
    event.preventDefault();
    moveObjective(from, to);
    $("objectives").querySelector(`[data-index="${to}"]`).focus();
}

function buildArguments(state) {
    return {
        ...state.request,
        objectives: state.objectives,
        constraints: state.constraints.filter(c => c.enabled).map(({ enabled, ...constraint }) => constraint),
        excluded: [...state.excluded],
        count: 3
    };
}

async function solve() {
    const call = ++state.lastCall;
    $("busy").hidden = false;
    try {
        const result = await request("tools/call", { name: "explore_trade_off", arguments: buildArguments(state) });
        if (call !== state.lastCall) return;
        if (result?.isError) {
            showError(result.content?.[0]?.text ?? "The solver refused these settings.");
            return;
        }
        state.selected = 1;
        show(result.structuredContent);
    } catch (error) {
        if (call === state.lastCall) showError(error?.message ?? "The host refused the call.");
    } finally {
        if (call === state.lastCall) $("busy").hidden = true;
    }
}

function showError(text) {
    $("error").textContent = text;
    $("error").hidden = false;
}

function select(rank) {
    state.selected = rank;
    drawResults(study);
}

function onOptionClick(event) {
    const target = event.target.closest("[data-rank]");
    if (target) select(Number(target.dataset.rank));
}

function drawPick(study) {
    const target = $("pick");
    target.replaceChildren();
    const option = study.best.find(o => o.rank === state.selected) ?? study.best[0];
    target.hidden = !option;
    if (!option) return;
    const heading = option.rank === 1 ? "Best option for these settings" : "Option " + option.rank + ", chosen by you";
    const button = el("button", "btn pri", "Use option " + option.rank);
    const note = el("div", "tr-note");
    button.addEventListener("click", () => sendChoice(option, button, note));
    target.append(el("div", "tr-name", heading + ": " + describe(option.choices)), button, note);
}

function buildMessage(option) {
    const lines = ["Use option " + option.rank + " of the trade-off \"" + titleOf(study) + "\":"];
    for (const choice of option.choices.filter(c => c.changed)) {
        const variant = state.choices.find(c => c.name === choice.choice).variants.find(v => v.name === choice.variant);
        const values = Object.entries(variant.values).map(([attribute, value]) => attribute + " " + withUnit(value, attribute));
        lines.push("- " + choice.choice + " (part " + choice.partId + "): variant " + choice.variant + " (" + values.join(", ") + ")");
    }
    const totals = option.totals.map(t => t.attribute + " " + withUnit(t.total, t.attribute) + " (" + signed(t.difference) + ")");
    lines.push("Totals: " + totals.join(", ") + ".");
    const changes = describeSettings(state);
    if (changes.length > 0) lines.push("Settings changed in the view: " + changes.join("; ") + ".");
    lines.push("Prepare the batch with apply_changes.");
    return lines.join("\n");
}

function describeSettings(state) {
    const changes = [...state.excluded].map(key => key + " turned off");
    const order = state.objectives.map(o => o.attribute).join(", ");
    if (order !== state.request.objectives.map(o => o.attribute).join(", ")) changes.push("objectives in order: " + order);
    state.constraints.forEach((constraint, index) => {
        const original = state.request.constraints[index];
        const name = constraint.name ?? constraint.attribute + " " + constraint.operator;
        if (!constraint.enabled) {
            changes.push(name + " turned off");
            return;
        }
        if (constraint.limit !== original.limit) changes.push(name + " limit " + constraint.limit + " " + (constraint.unit ?? ""));
        if ((constraint.margin ?? 0) !== (original.margin ?? 0)) changes.push(name + " margin " + constraint.margin + " %");
    });
    return changes;
}

async function sendChoice(option, button, note) {
    const content = [{ type: "text", text: buildMessage(option) }];
    button.disabled = true;
    try {
        const result = await request("ui/message", { role: "user", content });
        if (result?.isError) throw new Error("ui/message refused");
        note.textContent = "The message is in the chat box: check it and press Enter.";
    } catch {
        try {
            await request("ui/update-model-context", { content });
            note.textContent = "Sent to the assistant: type \"go\" in the chat.";
        } catch {
            note.textContent = "The host refused the message.";
            button.disabled = false;
            return;
        }
    }
    button.textContent = "Sent";
}

$("setup").addEventListener("change", onSetupChange);
$("objectives").addEventListener("pointerdown", onObjectiveGrab);
$("objectives").addEventListener("pointermove", onObjectiveDrag);
$("objectives").addEventListener("pointerup", onObjectiveDrop);
$("objectives").addEventListener("pointercancel", onObjectiveDrop);
$("objectives").addEventListener("keydown", onObjectiveKey);
$("options").addEventListener("click", onOptionClick);
$("chart").addEventListener("click", onOptionClick);
new ResizeObserver(sendSize).observe(document.body);
start();
