/**
 * Gets an element of the page.
 * @param {string} id The id of the element.
 * @returns {HTMLElement} The element, or null when the page has none with this id.
 */
const $ = id => document.getElementById(id);
//
// The study shown, as solve_trade_off or explore_trade_off gives it.
let study = null;

// What the user has set in the view: the request to send again, the variants turned off, the objectives in their order,
// the constraints with their switch, the selected option and the number of the last call to the solver.
let state = null;

/**
 * Starts the talk with the host (host.js), then asks for the full screen when the host offers it. Shows why when the host
 * refuses. Not async: the page is a classic script, where a call at the top level cannot be awaited.
 */
function start() {
    initializeView("trade-off")
        .then(context => {
            display.onHostContext(context);
            display.askFullscreen();
        })
        .catch(error => showError(error?.message ?? "The host did not start the view."));
}

/**
 * Lays the view out for a display mode: in full screen it fills the frame, and the link goes back to the conversation.
 * @param {string} mode The display mode given by the host.
 */
function showDisplayMode(mode) {
    $("view").classList.toggle("full", mode === "fullscreen");
    $("back").hidden = mode !== "fullscreen";
}

// The display mode of the view, which asks for the full screen when the host offers it (host.js).
const display = followDisplayMode(showDisplayMode, error => showError(error?.message ?? "The host did not change the display."));

/**
 * Creates an element.
 * @param {string} tag The name of the tag.
 * @param {string} [className] The CSS classes, or nothing.
 * @param {*} [text] The text of the element, or nothing.
 * @returns {HTMLElement} The element.
 */
function el(tag, className, text) {
    const element = document.createElement(tag);
    if (className) element.className = className;
    if (text !== undefined) element.textContent = text;
    return element;
}

// The drawings of the icons, inside a 24 × 24 box.
const ICONS = {
    grip: '<circle cx="9" cy="6" r="1"/><circle cx="15" cy="6" r="1"/><circle cx="9" cy="12" r="1"/><circle cx="15" cy="12" r="1"/><circle cx="9" cy="18" r="1"/><circle cx="15" cy="18" r="1"/>',
    excluded: '<circle cx="12" cy="12" r="9"/><path d="m15 9-6 6M9 9l6 6"/>'
};

/**
 * Creates an icon.
 * @param {string} name The name of the icon in ICONS.
 * @param {number} size The width and height of the icon, in pixels.
 * @returns {SVGElement} The icon.
 */
function icon(name, size) {
    const image = svg("svg", { class: "ic", width: size, height: size, viewBox: "0 0 24 24", fill: "none", stroke: "currentColor", "stroke-width": "1.75", "stroke-linecap": "round", "stroke-linejoin": "round" });
    image.innerHTML = ICONS[name];
    return image;
}

/**
 * Writes a number with 6 significant digits at most, without the noise of floating point (117.4, not 117.39999999).
 * @param {number} number The number.
 * @returns {string} The number as a text.
 */
function formatNumber(number) {
    return String(Number(number.toPrecision(6)));
}

/**
 * Writes a difference with its sign.
 * @param {number} number The difference.
 * @returns {string} The difference, for example "+8" or "−8.4".
 */
function signed(number) {
    return (number < 0 ? "−" : "+") + formatNumber(Math.abs(number));
}

/**
 * Gets the unit of a total.
 * @param {string} attribute The name of the attribute, for example mass.
 * @returns {string} The unit, or nothing when the attribute has none.
 */
function unitOf(attribute) {
    return study.attributes.find(a => a.name === attribute)?.unit;
}

/**
 * Writes a value of an attribute with its unit.
 * @param {number} value The value.
 * @param {string} attribute The name of the attribute.
 * @returns {string} The value, for example "117.4 kg".
 */
function withUnit(value, attribute) {
    const unit = unitOf(attribute);
    return unit ? formatNumber(value) + " " + unit : formatNumber(value);
}

/**
 * Writes the values of a variant or the totals of a combination.
 * @param {Object<string, number>} values The value of each attribute.
 * @returns {string} The values, for example "30 kg · 70".
 */
function valuesText(values) {
    return Object.entries(values).map(([attribute, value]) => withUnit(value, attribute)).join(" · ");
}

/**
 * Writes the variants of a combination.
 * @param {object[]} choices The variant taken for each choice, with its quantity.
 * @returns {string} The variants, for example "camera: A · solarPanel: large × 2".
 */
function describe(choices) {
    return choices.map(c => c.choice + ": " + c.variant + (c.quantity > 1 ? " × " + c.quantity : "")).join(" · ");
}

/**
 * Gives a key to a combination, to find a best option among all the points.
 * @param {object[]} choices The variant taken for each choice, with its quantity.
 * @returns {string} The key.
 */
function keyOf(choices) {
    return choices.map(c => c.variant + " × " + c.quantity).join(" | ");
}

/**
 * Gets the number of a best option, as the list shows it.
 * @param {object} option The best option.
 * @returns {number} The number, from 1.
 */
function optionNumber(option) {
    return study.best.indexOf(option) + 1;
}

/**
 * Gets the title of a study: the title given by the assistant, or the names of the choices.
 * @param {object} study The study.
 * @returns {string} The title.
 */
function titleOf(study) {
    return study.title ?? "Trade-off · " + study.choices.map(choice => choice.name).join(", ");
}

// The text of each status of the solver.
const STATUS_TEXT = {
    Optimal: "best options found",
    Feasible: "good options found, maybe not the best",
    Infeasible: "the rules leave no combination",
    Unknown: "the solver stopped before finding an answer"
};

/**
 * Shows the result of solve_trade_off, and keeps its settings as the starting point of the view; without a study, tells why.
 * @param {object} result The result of the tool, with the study as structured content.
 */
function onFirstStudy(result) {
    const first = result?.structuredContent;
    if (!first) {
        $("title").textContent = "The trade-off could not be computed";
        $("grid").hidden = true;
        showError(result?.content?.[0]?.text ?? "The host sent no trade-off to show.");
        return;
    }
    state = {
        request: structuredClone(first.request),
        choices: first.choices,
        excluded: new Set(first.request.excluded ?? []),
        objectives: first.request.objectives.map(objective => ({ ...objective })),
        constraints: first.request.constraints.map(constraint => ({ ...constraint, enabled: true })),
        requirements: first.requirements ?? [],
        otherRequirementCount: first.otherRequirementCount ?? 0,
        ignored: new Set(first.request.ignoredRequirements ?? []),
        selected: 1,
        lastCall: 0
    };
    show(first);
    drawSetup(state);
}

/**
 * Shows a new study, without the error of the last call.
 * @param {object} newStudy The study.
 */
function show(newStudy) {
    study = newStudy;
    $("error").hidden = true;
    drawResults(study);
}

/**
 * Draws what depends on the result: the header, the chart, the best options, the excluded combinations and the selected option.
 * @param {object} study The study.
 */
function drawResults(study) {
    drawHeader(study);
    drawChart(study);
    drawOptions(study);
    drawExcluded(study);
    drawPick(study);
}

/**
 * Draws the title, the root, the number of combinations and the status of the solver.
 * @param {object} study The study.
 */
function drawHeader(study) {
    $("title").textContent = titleOf(study);
    const count = study.pointsComplete ? study.points.length + " combinations" : "too many combinations to show them all";
    $("subtitle").textContent = [study.root.qualifiedName, count, STATUS_TEXT[study.status] ?? study.status].join(" · ");
}

/**
 * Draws the totals of today and the best options, numbered from 1, the selected one highlighted.
 * @param {object} study The study.
 */
function drawOptions(study) {
    const target = $("options");
    target.replaceChildren();
    $("today").textContent = "vs today: " + study.current.totals.map(total => labelOf(total, study.current.totals) + withUnit(total.total, total.attribute)).join(" · ");
    $("closest").hidden = study.best.length === 0 || countUnmet(study.best[0]) === 0;

    study.best.forEach((option, index) => {
        const number = index + 1;
        const card = el("div", number === state.selected ? "tr-option on" : "tr-option");
        card.dataset.option = number;
        const body = el("div");
        body.append(el("div", "tr-name", describe(option.choices)), drawTotals(option.totals), drawChecks(option.constraints, option.requirements ?? []));
        card.append(el("span", "tr-rank n" + option.rank, number), body);
        target.append(card);
    });
    if (study.best.length === 0) target.append(el("div", "tr-empty", "The rules leave no combination."));
}

/**
 * Counts what a combination does not meet: the constraints that it breaks and the requirements of the model that do not
 * hold on it, or that the server cannot check.
 * @param {object} option The combination.
 * @returns {number} The number of constraints and requirements not met.
 */
function countUnmet(option) {
    return option.constraints.filter(check => !check.satisfied).length + (option.requirements ?? []).filter(requirement => requirement.status !== "Satisfied").length;
}

/**
 * Writes the name of a total before its value when the totals are more than two: two totals read in the order of the
 * objectives, more do not.
 * @param {object} total The total.
 * @param {object[]} totals The totals shown together.
 * @returns {string} The name followed by a space, or nothing.
 */
function labelOf(total, totals) {
    return totals.length > 2 ? total.attribute + " " : "";
}

/**
 * Draws the totals of a combination, each with its difference from today.
 * @param {object[]} totals The totals.
 * @returns {HTMLElement} The line of totals.
 */
function drawTotals(totals) {
    const line = el("div", "tr-totals");
    totals.forEach((total, index) => {
        if (index > 0) line.append(" · ");
        line.append(labelOf(total, totals) + withUnit(total.total, total.attribute) + " ", drawDifference(total));
    });
    return line;
}

/**
 * Draws the difference of a total from today: green when it goes the way of its objective, red otherwise.
 * @param {object} total The total, with its difference.
 * @returns {HTMLElement} The difference.
 */
function drawDifference(total) {
    if (total.difference === 0) return el("span", null, "(=)");
    const objective = study.objectives.find(o => o.attribute === total.attribute);
    const better = objective?.direction === "Maximize" ? total.difference > 0 : total.difference < 0;
    return el("span", better ? "tr-better" : "tr-worse", "(" + signed(total.difference) + ")");
}

/**
 * Draws the constraints of a combination, met or not, with the compared value and the limit, then the requirements of the
 * model: each one that the combination breaks or that the server cannot check, with the reason, and the number of those met.
 * @param {object[]} constraints The checks of the constraints.
 * @param {object[]} requirements What the combination does to the requirements of the model.
 * @returns {HTMLElement} The line of checks.
 */
function drawChecks(constraints, requirements) {
    const line = el("div", "tr-checks");
    for (const check of constraints) {
        let text = (check.satisfied ? "✓ " : "✗ ") + (check.name ?? check.constraint);
        if (check.value != null) text += " · " + formatNumber(check.value) + " / " + formatNumber(check.limit);
        line.append(el("span", check.satisfied ? "tagv ok" : "tagv bad", text));
    }
    for (const requirement of requirements.filter(outcome => outcome.status !== "Satisfied")) {
        const broken = requirement.status === "NotSatisfied";
        line.append(el("span", broken ? "tagv bad" : "tagv warn", (broken ? "✗ " : "? ") + requirement.name + " · " + requirement.explanation));
    }
    const met = requirements.filter(outcome => outcome.status === "Satisfied");
    if (met.length > 0) {
        const tag = el("span", "tagv ok", "✓ " + met.length + (met.length === 1 ? " requirement" : " requirements"));
        tag.title = met.map(outcome => outcome.name + ": " + outcome.explanation).join("\n");
        line.append(tag);
    }
    return line;
}

/**
 * Draws the combinations that break a constraint or a rule, with the reasons, and the conflicts when no combination works.
 * @param {object} study The study.
 */
function drawExcluded(study) {
    const target = $("excluded");
    target.replaceChildren();
    const shown = new Set(study.best.map(option => keyOf(option.choices)));
    const rejected = study.points.filter(point => !point.feasible && !shown.has(keyOf(point.choices)));
    if (rejected.length > 0) {
        target.append(el("h3", null, "Excluded (" + rejected.length + ")"));
        const list = el("div", "tr-scroll");
        list.tabIndex = 0;
        list.ariaLabel = "Excluded combinations";
        for (const point of rejected) list.append(drawRejected(describe(point.choices), point.broken.join(" · ")));
        target.append(list);
    }
    if (study.conflicts.length > 0) {
        target.append(el("h3", null, "Why no combination works"));
        for (const conflict of study.conflicts) target.append(drawRejected(null, conflict));
    }
}

/**
 * Draws an excluded combination or a conflict.
 * @param {string} name The variants of the combination, or null for a conflict.
 * @param {string} reason Why it is excluded.
 * @returns {HTMLElement} The line.
 */
function drawRejected(name, reason) {
    const text = el("span");
    if (name) text.append(el("b", null, name), " · ");
    text.append(reason);
    const line = el("div", "tr-rejected");
    line.append(icon("excluded", 13), text);
    return line;
}

// The namespace of the SVG elements, and the box of the chart: its size and the edges of the area of the points.
const SVG_NS = "http://www.w3.org/2000/svg";
const BOX = { width: 560, height: 320, left: 56, right: 544, top: 16, bottom: 276 };

/**
 * Creates an SVG element.
 * @param {string} tag The name of the tag.
 * @param {Object<string, *>} attributes The attributes.
 * @param {*} [text] The text of the element, or nothing.
 * @returns {SVGElement} The element.
 */
function svg(tag, attributes, text) {
    const element = document.createElementNS(SVG_NS, tag);
    for (const [name, value] of Object.entries(attributes)) element.setAttribute(name, value);
    if (text !== undefined) element.textContent = text;
    return element;
}

/**
 * Makes the scale of an axis: the values, with 10 % of space on each side, go from one end of the axis to the other.
 * @param {number[]} values The values shown on the axis.
 * @param {number} from The position of the smallest value, in the box of the chart.
 * @param {number} to The position of the largest value, in the box of the chart.
 * @returns {{min: number, max: number, at: function(number): number}} The ends of the axis, and the position of a value.
 */
function makeScale(values, from, to) {
    let min = Math.min(...values);
    let max = Math.max(...values);
    const padding = (max - min) * 0.1 || 1;
    min -= padding;
    max += padding;
    return { min, max, at: value => from + (value - min) / (max - min) * (to - from) };
}

/**
 * Chooses the step between two ticks: 1, 2 or 5 times a power of 10, for about 6 ticks.
 * @param {number} range The range of the axis.
 * @returns {number} The step.
 */
function niceStep(range) {
    const raw = range / 6;
    const power = 10 ** Math.floor(Math.log10(raw));
    return [1, 2, 5, 10].map(factor => factor * power).find(step => step >= raw);
}

/**
 * Gets the values of the ticks of an axis.
 * @param {{min: number, max: number}} scale The scale of the axis.
 * @returns {number[]} The values, multiples of the step.
 */
function ticks(scale) {
    const step = niceStep(scale.max - scale.min);
    const values = [];
    for (let value = Math.ceil(scale.min / step) * step; value <= scale.max; value += step) values.push(value);
    return values;
}

/**
 * Gets the totals of a combination by attribute.
 * @param {object[]} totals The totals.
 * @returns {Object<string, number>} The total of each attribute.
 */
function totalsByName(totals) {
    return Object.fromEntries(totals.map(total => [total.attribute, total.total]));
}

/**
 * Gets the name of an axis, with its unit.
 * @param {string} attribute The name of the attribute.
 * @returns {string} The name, for example "mass [kg]".
 */
function axisName(attribute) {
    const unit = unitOf(attribute);
    return unit ? attribute + " [" + unit + "]" : attribute;
}

/**
 * Draws every combination on the first two attributes (objectives first), with today, the limits of the constraints on
 * these attributes and the forbidden areas; the best options carry their number, the selected one drawn on top.
 * @param {object} study The study.
 */
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
    const limits = [...study.constraints, ...(study.requirements ?? []).filter(requirement => !requirement.ignored)]
        .filter(limit => limit.threshold != null && (limit.attribute === xName || limit.attribute === yName));
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

    const numbers = new Map(study.best.map((option, index) => [keyOf(option.choices), index + 1]));
    const numberOf = point => numbers.get(keyOf(point.choices));
    const others = study.points.filter(point => numberOf(point) === undefined);
    const best = study.points.filter(point => numberOf(point) !== undefined).sort((a, b) => numberOf(b) - numberOf(a));
    const selected = best.findIndex(point => numberOf(point) === state.selected);
    if (selected >= 0) best.push(...best.splice(selected, 1));
    for (const point of [...others, ...best]) chart.append(drawPoint(point, numberOf(point), x.at(point.totals[xName]), y.at(point.totals[yName])));

    chart.append(svg("text", { x: todayX, y: todayY - 16, "text-anchor": "middle" }, "today"));
    target.append(chart);
    drawLegend(limits);
}

/**
 * Draws the legend of the chart: the kinds of points, and one line for each limit drawn.
 * @param {object[]} limits The constraints whose limit is drawn.
 */
function drawLegend(limits) {
    const item = (className, text) => {
        const span = el("span");
        span.append(el("i", className), text);
        return span;
    };
    $("legend").replaceChildren(item("best", "Best options"), item("ok", "Meets everything"), item("bad", "Breaks a requirement, a constraint or a rule"));
    for (const limit of limits) $("legend").append(item("limit", (limit.name ?? limit.attribute) + " limit"));
}

/**
 * Draws the limit of a constraint, its label, and the area beyond it; an equality has no such area, only its line.
 * @param {SVGElement} chart The chart.
 * @param {object} limit The constraint, with its threshold.
 * @param {boolean} onX true when the constraint is on the attribute of the horizontal axis.
 * @param {{min: number, max: number, at: function(number): number}} scale The scale of this axis.
 */
function drawLimit(chart, limit, onX, scale) {
    const at = scale.at(limit.threshold);
    const end = scale.at(limit.operator.startsWith("<") ? scale.max : scale.min);
    const from = Math.min(at, end);
    const size = Math.abs(end - at);
    const label = (limit.name ?? limit.description) + " · " + withUnit(limit.threshold, limit.attribute);
    const area = onX
        ? { x: from, y: BOX.top, width: size, height: BOX.bottom - BOX.top }
        : { x: BOX.left, y: from, width: BOX.right - BOX.left, height: size };
    if (limit.operator !== "==") chart.append(svg("rect", { ...area, class: "tr-forbidden" }));
    if (onX) {
        chart.append(svg("line", { x1: at, x2: at, y1: BOX.top, y2: BOX.bottom, class: "tr-limit" }));
        chart.append(svg("text", { x: at - 4, y: BOX.top + 12, "text-anchor": "end", class: "tr-limit-label" }, label));
    } else {
        chart.append(svg("line", { x1: BOX.left, x2: BOX.right, y1: at, y2: at, class: "tr-limit" }));
        chart.append(svg("text", { x: BOX.right - 4, y: at - 4, "text-anchor": "end", class: "tr-limit-label" }, label));
    }
}

/**
 * Draws a combination: a best option with its number, a combination that meets the constraints, or one that breaks them;
 * its tooltip gives its variants, its totals and why it is excluded.
 * @param {object} point The combination.
 * @param {number} number The number of the best option, or undefined for another combination.
 * @param {number} cx The horizontal position of the point.
 * @param {number} cy The vertical position of the point.
 * @returns {SVGElement} The point.
 */
function drawPoint(point, number, cx, cy) {
    const isBest = number !== undefined;
    let className = "pt pt-ok";
    if (!point.feasible) className = "pt pt-bad";
    else if (isBest) className = "pt pt-best pt-rank" + study.best[number - 1].rank;
    const group = svg("g", { class: className });
    const tooltip = [describe(point.choices), valuesText(point.totals), ...point.broken];
    if (isBest) tooltip.unshift("Option " + number);
    group.append(svg("title", {}, tooltip.join(" · ")));
    if (isBest && number === state.selected) group.append(svg("circle", { cx, cy, r: 13, class: "tr-ring" }));
    group.append(svg("circle", { cx, cy, r: 8 }));
    if (isBest) {
        group.dataset.option = number;
        group.append(svg("text", { x: cx, y: cy + 3.5, "text-anchor": "middle" }, number));
    }
    if (!point.feasible) group.append(svg("path", { d: `M${cx - 3} ${cy - 3}l6 6M${cx + 3} ${cy - 3}l-6 6` }));
    return group;
}

/**
 * Draws the settings that the user can change: the variants, the objectives and the constraints.
 * @param {object} state The settings of the view.
 */
function drawSetup(state) {
    drawChoices(state);
    drawObjectives(state);
    drawConstraints(state);
    drawRequirements(state);
}

// How the state of a requirement today is shown: the class of its tag and its text.
const REQUIREMENT_STATES = {
    Satisfied: ["tagv ok", "met today"],
    NotSatisfied: ["tagv bad", "broken today"]
};

/**
 * Draws the requirements of the model that the server checks on every combination: each one with its state today and its
 * constraint, and a switch that turns it off when a choice can change it.
 * @param {object} state The settings of the view.
 */
function drawRequirements(state) {
    const target = $("requirements");
    target.replaceChildren();
    if (state.requirements.length === 0 && state.otherRequirementCount === 0) return;
    target.append(el("h3", null, "Requirements of the model"));
    for (const requirement of state.requirements) {
        const label = el("label");
        if (requirement.dependsOnChoices || requirement.ignored) {
            const toggle = el("input", "tr-switch");
            toggle.type = "checkbox";
            toggle.role = "switch";
            toggle.checked = !state.ignored.has(requirement.name);
            toggle.dataset.kind = "requirement";
            toggle.dataset.key = requirement.name;
            label.append(toggle);
        }
        const [className, text] = REQUIREMENT_STATES[requirement.status] ?? ["tagv warn", "not checked"];
        label.append(el("span", null, requirement.name), el("span", className, text));
        label.title = requirement.text ?? "";

        let detail = requirement.constraint ?? "";
        if (!requirement.dependsOnChoices) detail += " · no choice changes it";
        else if (!requirement.searched) detail += " · checked on each combination";

        const line = el("div", "tr-constraint");
        line.append(label, el("div", "tr-limits", detail));
        target.append(line);
    }
    if (state.otherRequirementCount > 0) {
        target.append(el("div", "tr-note", state.otherRequirementCount + " more hold today and do not depend on these choices."));
    }
}

/**
 * Draws each choice with its variants, each with a box to turn it off and its values.
 * @param {object} state The settings of the view.
 */
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
            label.append(box, el("span", null, variantLabel(variant)), el("span", "tr-values", valuesText(variant.values)));
            target.append(label);
        }
    }
}

/**
 * Names a variant in the set-up: the variant of a catalogue that the model takes keeps its name, marked as current.
 * @param {object} variant The variant.
 * @returns {string} The name shown.
 */
function variantLabel(variant) {
    if (!variant.isCurrent) return variant.name;
    return variant.name === "current" ? "Current" : variant.name + " (current)";
}

/**
 * Draws the objectives in their order; with two or more, each line can be dragged or moved with the arrow keys.
 * @param {object} state The settings of the view.
 */
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

// The signs of the operators of the constraints.
const OPERATORS = { "<=": "≤", ">=": "≥", "==": "=" };

/**
 * Draws each constraint with its switch, its limit (a number, or the attribute it is compared to) and its margin.
 * @param {object} state The settings of the view.
 */
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

/**
 * Creates the field of a number of a constraint.
 * @param {string} kind What the number is: limit or margin.
 * @param {number} index The index of the constraint.
 * @param {number} value The number.
 * @returns {HTMLInputElement} The field.
 */
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

/**
 * Keeps a setting changed by the user (a variant turned off or on, a constraint switched, a limit or a margin), then solves
 * again; an empty number is ignored.
 * @param {Event} event The change event of a field of the set-up.
 */
function onSetupChange(event) {
    const input = event.target;
    const kind = input.dataset.kind;
    if (kind === "variant") {
        if (input.checked) state.excluded.delete(input.dataset.key);
        else state.excluded.add(input.dataset.key);
    } else if (kind === "requirement") {
        if (input.checked) state.ignored.delete(input.dataset.key);
        else state.ignored.add(input.dataset.key);
    } else if (kind === "enabled") {
        state.constraints[Number(input.dataset.key)].enabled = input.checked;
    } else if (kind === "limit" || kind === "margin") {
        if (input.value === "") return;
        state.constraints[Number(input.dataset.key)][kind] = Number(input.value);
    } else {
        return;
    }
    void solve();
}

/**
 * Moves an objective to another place in the order, then solves again.
 * @param {number} from The index of the objective.
 * @param {number} to Its new index.
 */
function moveObjective(from, to) {
    const [objective] = state.objectives.splice(from, 1);
    state.objectives.splice(to, 0, objective);
    drawObjectives(state);
    void solve();
}

// The objective being dragged: its line, its index when grabbed, and where the pointer holds it.
let drag = null;

/**
 * Grabs an objective with the main button of the pointer.
 * @param {PointerEvent} event The pointerdown event.
 */
function onObjectiveGrab(event) {
    const line = event.target.closest(".tr-objective[data-index]");
    if (!line || event.button !== 0) return;
    line.setPointerCapture(event.pointerId);
    line.classList.add("dragging");
    drag = { line, from: Number(line.dataset.index), offset: event.clientY - line.getBoundingClientRect().top };
}

/**
 * Moves the dragged objective among the others as the pointer passes their middle, renumbers them, and keeps the line under
 * the pointer.
 * @param {PointerEvent} event The pointermove event.
 */
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

/**
 * Gets the vertical middle of an element.
 * @param {HTMLElement} element The element.
 * @returns {number} The middle, in pixels from the top of the view.
 */
function middleOf(element) {
    const box = element.getBoundingClientRect();
    return box.top + box.height / 2;
}

/**
 * Drops the dragged objective: at its new place when the pointer is released, back at its place when the drag is cancelled.
 * @param {PointerEvent} event The pointerup or pointercancel event.
 */
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

/**
 * Moves the objective that has the focus up or down with the arrow keys, and keeps the focus on it.
 * @param {KeyboardEvent} event The keydown event.
 */
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

/**
 * Builds the arguments of explore_trade_off: the request of the study with the settings of the view.
 * @param {object} state The settings of the view.
 * @returns {object} The arguments, with the constraints turned on only.
 */
function buildArguments(state) {
    return {
        ...state.request,
        objectives: state.objectives,
        constraints: state.constraints.filter(c => c.enabled).map(({ enabled, ...constraint }) => constraint),
        excluded: [...state.excluded],
        ignoredRequirements: [...state.ignored]
    };
}

/**
 * Runs the trade-off again with the settings of the view, through the host, and shows the new study or the error; only the
 * answer to the last call counts.
 */
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

/**
 * Shows an error above the cards.
 * @param {string} text The error.
 */
function showError(text) {
    $("error").textContent = text;
    $("error").hidden = false;
}

/**
 * Selects a best option.
 * @param {number} number The number of the option.
 */
function select(number) {
    state.selected = number;
    drawResults(study);
}

/**
 * Selects the best option clicked, in the list or on the chart.
 * @param {MouseEvent} event The click event.
 */
function onOptionClick(event) {
    const target = event.target.closest("[data-option]");
    if (target) select(Number(target.dataset.option));
}

/**
 * Draws the selected option with the button that sends it to the assistant; hidden when there is no best option.
 * @param {object} study The study.
 */
function drawPick(study) {
    const target = $("pick");
    target.replaceChildren();
    const option = study.best[state.selected - 1] ?? study.best[0];
    target.hidden = !option;
    if (!option) return;
    const number = optionNumber(option);
    const button = el("button", "btn pri", "Use option " + number);
    const note = el("div", "tr-note");
    button.addEventListener("click", () => sendChoice(option, button, note));
    target.append(el("div", "tr-name", headingOf(option, number) + ": " + describe(option.choices)), button, note);
}

/**
 * Gets the heading of the selected option.
 * @param {object} option The option.
 * @param {number} number The number of the option.
 * @returns {string} The heading.
 */
function headingOf(option, number) {
    return option.rank === 1 ? "Best option for these settings" : "Option " + number + ", chosen by you";
}

/**
 * Writes the message that asks the assistant to use an option: each variant changed with its part and its values, the
 * totals, and the settings changed in the view.
 * @param {object} option The option.
 * @returns {string} The message.
 */
function buildMessage(option) {
    const lines = ["Use option " + optionNumber(option) + " of the trade-off \"" + titleOf(study) + "\":"];
    for (const choice of option.choices.filter(c => c.changed)) {
        const variant = state.choices.find(c => c.name === choice.choice).variants.find(v => v.name === choice.variant);
        const values = Object.entries(variant.values).map(([attribute, value]) => attribute + " " + withUnit(value, attribute));
        const units = choice.quantity === 1 ? "" : " × " + choice.quantity;
        lines.push("- " + choice.choice + " (part " + choice.partId + "): variant " + choice.variant + units + " (" + values.join(", ") + ")");
    }
    const totals = option.totals.map(t => t.attribute + " " + withUnit(t.total, t.attribute) + " (" + signed(t.difference) + ")");
    lines.push("Totals: " + totals.join(", ") + ".");
    const unmet = (option.requirements ?? []).filter(outcome => outcome.status !== "Satisfied").map(outcome => outcome.name + " (" + outcome.explanation + ")");
    if (unmet.length > 0) lines.push("Requirements not met: " + unmet.join("; ") + ".");
    const changes = describeSettings(state);
    if (changes.length > 0) lines.push("Settings changed in the view: " + changes.join("; ") + ".");
    lines.push("Prepare the batch with apply_changes.");
    return lines.join("\n");
}

/**
 * Describes the settings that the user has changed since the result of solve_trade_off.
 * @param {object} state The settings of the view.
 * @returns {string[]} The changes, for example "camera.A turned off".
 */
function describeSettings(state) {
    const changes = [...state.excluded, ...state.ignored].map(key => key + " turned off");
    const order = state.objectives.map(o => o.attribute).join(", ");
    if (order !== state.request.objectives.map(o => o.attribute).join(", ")) changes.push("objectives in order: " + order);
    state.constraints.forEach((constraint, index) => {
        const original = state.request.constraints[index];
        const name = constraint.name ?? constraint.attribute + " " + constraint.operator;
        if (!constraint.enabled) {
            changes.push(name + " turned off");
            return;
        }
        if (constraint.limit !== original.limit) changes.push(name + " limit " + constraint.limit + (constraint.unit ? " " + constraint.unit : ""));
        if ((constraint.margin ?? 0) !== (original.margin ?? 0)) changes.push(name + " margin " + constraint.margin + " %");
    });
    return changes;
}

/**
 * Sends the message of an option to the conversation: put in the chat box when the host allows it, otherwise given to the
 * assistant as context; the button is disabled once sent.
 * @param {object} option The option.
 * @param {HTMLButtonElement} button The button of the option.
 * @param {HTMLElement} note Where to tell the user what to do next.
 */
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

// The events of the view: settings changed, objectives dragged or moved with the keys, options clicked in the list or on
// the chart, and the size of the page.
$("setup").addEventListener("change", onSetupChange);
$("back").addEventListener("click", () => display.ask("inline"));
$("objectives").addEventListener("pointerdown", onObjectiveGrab);
$("objectives").addEventListener("pointermove", onObjectiveDrag);
$("objectives").addEventListener("pointerup", onObjectiveDrop);
$("objectives").addEventListener("pointercancel", onObjectiveDrop);
$("objectives").addEventListener("keydown", onObjectiveKey);
$("options").addEventListener("click", onOptionClick);
$("chart").addEventListener("click", onOptionClick);
listenToHost(onFirstStudy, display.onHostContext);
new ResizeObserver(sendSize).observe(document.body);
start();
