/**
 * Gets an element of the page.
 * @param {string} id The id of the element.
 * @returns {HTMLElement} The element, or null when the page has none with this id.
 */
const $ = id => document.getElementById(id);

// The width from which the card stands next to the table; below it, the card unfolds under its row.
const WIDE_VIEW = 1180;

// The widths up to which the table hides columns, as the style sheet does.
const NARROW_VIEW = 760;
const VERY_NARROW_VIEW = 480;

// The options of import_requirements, and the choice of the card that imports nothing.
const AS_WRITTEN = "AsWritten";
const WITH_REWRITE = "WithRewrite";
const AS_GOAL = "AsGoal";
const MERGE_INTO = "MergeInto";
const LEAVE_OUT = "LeaveOut";

// The tool that imports and the tool that saves.
const IMPORT_TOOL = "import_requirements";
const SAVE_TOOL = "save_requirement_list";

// The findings of the server, as the column "Quality" names them.
const FINDING_LABELS = {
    NotInSection: "Not in section",
    AlreadyModelled: "Already modelled",
    Repeated: "Duplicate",
    InvalidChange: "Refused",
    InvalidRewrite: "Rewrite refused",
    InvalidMerge: "Merge refused",
    ToClarify: "To clarify"
};

// The findings that forbid the import of a requirement.
const BLOCKING = new Set(["AlreadyModelled", "Repeated"]);

// The icons of the page: the paths of the mock-up, on a grid of 24.
const ICONS = {
    check: "M20 6 9 17l-5-5",
    warn: "M10.3 3.9 1.9 18a2 2 0 0 0 1.7 3h16.8a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0M12 9v4.5M12 17.2h.01",
    previous: "m15 6-6 6 6 6",
    next: "m9 6 6 6-6 6"
};

// What each column sorts on.
const SORT_KEYS = {
    id: row => row.requirement.reqId,
    requirement: row => row.requirement.text,
    kind: row => row.requirement.kind ?? "",
    constraint: row => constraintLabel(row),
    quality: row => (row.isReady ? "0" : "1") + qualityLabel(row)
};

// The card of the selected requirement: next to the table, or moved under its row when the view is narrow.
const detailCard = $("detail");

// What the view knows: the proposal kept by the server; what is applied to each requirement (by reqId: its option, its target and
// whether it is ticked); what is picked in its card and not applied yet; the filter and the sort of the table; the selected row;
// the room and the display mode; and where the import stands.
const state = {
    proposal: null,
    applied: new Map(),
    picks: new Map(),
    filter: "all",
    sort: null,
    selected: null,
    narrow: window.innerWidth < WIDE_VIEW,
    columns: visibleColumns(),
    busy: false,
    importedText: null
};

/**
 * Counts the columns that the table shows at the width of the view: a row that spans hidden columns would leave them empty.
 * @returns {number} The number of columns.
 */
function visibleColumns() {
    if (window.innerWidth <= VERY_NARROW_VIEW) return 3;
    return window.innerWidth <= NARROW_VIEW ? 4 : 6;
}

/**
 * Starts the talk with the host, then asks for the full screen when the host offers it.
 */
function start() {
    initializeView("requirement-extraction")
        .then(context => {
            display.onHostContext(context);
            display.askFullscreen();
        })
        .catch(error => showError("The host did not start the view: " + error.message));
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
const display = followDisplayMode(showDisplayMode, error => showError("The host did not change the display: " + error.message));

/**
 * Shows the proposal sent by propose_requirements: the ready requirements are ticked, the others wait for a choice of the user, and
 * the choices saved for this section come back when they still apply.
 * @param {object} result The result of the tool.
 */
function onToolResult(result) {
    const proposal = result?.structuredContent;
    if (!Array.isArray(proposal?.requirements)) {
        showError(result?.isError ? result.content?.[0]?.text ?? "propose_requirements has failed." : "The host sent no proposal to the view: the assistant goes on in the chat.");
        return;
    }
    state.proposal = proposal;
    state.applied = new Map(proposal.requirements.filter(row => row.isReady).map(row => [row.requirement.reqId, { option: AS_WRITTEN, ticked: true }]));
    state.picks = new Map();
    state.importedText = null;
    showError(null);
    $("note").textContent = restoreSavedRows(proposal);
    state.selected = firstToShow(rowsInOrder());
    render();
}

/**
 * Restores the rows that the user has saved for this section, on the findings computed again.
 * @param {object} proposal The proposal.
 * @returns {string} What the user must know about it, or an empty text when nothing has been saved.
 */
function restoreSavedRows(proposal) {
    const saved = (proposal.savedRows ?? []).filter(item => item.option);
    let restored = 0;
    for (const item of saved) {
        const row = proposal.requirements.find(candidate => !candidate.isBlocked && candidate.requirement.reqId === item.reqId);
        if (row && isStillValid(row, item)) {
            state.applied.set(item.reqId, { option: item.option, mergeInto: item.mergeInto, ticked: item.ticked });
            restored++;
        }
    }
    if (saved.length === 0) return "";
    const lost = saved.length - restored;
    return `Saved list restored: ${plural(restored, "choice")} kept` + (lost === 0 ? "." : `, ${lost} no longer valid.`);
}

/**
 * Tells whether a saved choice still applies to a row, whose findings have been computed again.
 * @param {object} row The row.
 * @param {object} item The saved row.
 * @returns {boolean} true when the choice still applies.
 */
function isStillValid(row, item) {
    switch (item.option) {
        case AS_WRITTEN:
            return row.isReady;
        case WITH_REWRITE:
            return !row.isReady && hasValidRewrite(row);
        case AS_GOAL:
            return !row.isReady;
        case MERGE_INTO:
            return !row.isReady && mergeCandidates(row).some(candidate => candidate.requirement.reqId === item.mergeInto);
        default:
            return false;
    }
}

/**
 * Shows an error above the table, or hides it.
 * @param {string} text The error, or null to hide it.
 */
function showError(text) {
    $("error").hidden = !text;
    $("error").textContent = text ?? "";
}

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

/**
 * Creates an icon of the page.
 * @param {string} name The name of the icon.
 * @param {number} size The size of the icon, in pixels.
 * @returns {SVGElement} The icon.
 */
function icon(name, size) {
    const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
    const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
    path.setAttribute("d", ICONS[name]);
    for (const [attribute, value] of Object.entries({ class: "ic", width: size, height: size, viewBox: "0 0 24 24", fill: "none", stroke: "currentColor", "stroke-width": 2, "stroke-linecap": "round", "stroke-linejoin": "round" })) {
        svg.setAttribute(attribute, value);
    }
    svg.append(path);
    return svg;
}

/**
 * Writes a number of things, with the plural when needed.
 * @param {number} count The number.
 * @param {string} word The thing, in the singular.
 * @returns {string} For example "1 requirement" or "3 requirements".
 */
function plural(count, word) {
    return `${count} ${word}${count === 1 ? "" : "s"}`;
}

/**
 * Reduces the white space of a text to single spaces, as the server does for the passages of the document.
 * @param {string} text The text.
 * @returns {string} The text, with single spaces between its words.
 */
function normalize(text) {
    return text.split(/\s+/).filter(Boolean).join(" ");
}

/**
 * Writes a constraint as the mock-up does: the attribute, the margin, the operator and the limit with its unit.
 * @param {object} constraint The constraint, as apply_changes takes it.
 * @returns {string} For example "mass * 1.2 <= 150 [kg]".
 */
function constraintText(constraint) {
    const factor = constraint.margin ? " * " + Number((1 + constraint.margin / 100).toFixed(6)) : "";
    const limit = constraint.limitAttribute ?? String(constraint.limit) + (constraint.unit ? ` [${constraint.unit}]` : "");
    return constraint.operator.startsWith("<")
        ? `${constraint.attribute}${factor} ${constraint.operator} ${limit}`
        : `${constraint.attribute} ${constraint.operator} ${limit}${factor}`;
}

/**
 * Gets what is applied to a row.
 * @param {object} row The row: a requirement of the proposal, with its findings.
 * @returns {object} Its option, its target and whether it is ticked, or null when nothing is applied.
 */
function appliedOf(row) {
    return row.isBlocked ? null : state.applied.get(row.requirement.reqId) ?? null;
}

/**
 * Tells whether a row needs a choice of the user: it has a problem and it can still be imported.
 * @param {object} row The row.
 * @returns {boolean} true when the card of the row asks what to do.
 */
function isToReview(row) {
    return !row.isReady && !row.isBlocked;
}

/**
 * Gets the constraint that a row gets with what is applied to it.
 * @param {object} row The row.
 * @returns {object} The constraint, or null when the row gets none.
 */
function constraintOf(row) {
    switch (appliedOf(row)?.option) {
        case AS_GOAL:
        case MERGE_INTO:
            return null;
        case WITH_REWRITE:
            return row.requirement.rewrittenConstraint ?? null;
        default:
            return row.requirement.constraint ?? null;
    }
}

/**
 * Writes what the column "Verifiable as" shows for a row.
 * @param {object} row The row.
 * @returns {string} The constraint, the requirement that receives the row, or "no threshold".
 */
function constraintLabel(row) {
    const applied = appliedOf(row);
    if (applied?.option === MERGE_INTO) return "merged into " + applied.mergeInto;
    const constraint = constraintOf(row);
    return constraint ? constraintText(constraint) : "no threshold";
}

/**
 * Tells whether the rewrite of a row can be imported: the assistant wrote one, and the server accepts it.
 * @param {object} row The row.
 * @returns {boolean} true when the rewrite can be imported.
 */
function hasValidRewrite(row) {
    return Boolean(row.requirement.rewrittenText) && !row.findings.some(finding => finding.kind === "InvalidRewrite");
}

/**
 * Gets the rows that can receive a row: the other rows of the table, except the blocked ones.
 * @param {object} row The row.
 * @returns {object[]} The rows, in the order of the proposal.
 */
function mergeCandidates(row) {
    return state.proposal.requirements.filter(other => !other.isBlocked && other.requirement.reqId !== row.requirement.reqId);
}

/**
 * Gets the rows that the user imports.
 * @returns {object[]} The rows, in the order of the proposal.
 */
function chosenRows() {
    return state.proposal.requirements.filter(row => appliedOf(row)?.ticked);
}

/**
 * Tells why the rows that the user imports cannot be merged as chosen.
 * @returns {string} The first problem, or null when every merged row has a target imported by the same batch.
 */
function mergeProblem() {
    for (const row of chosenRows()) {
        const target = appliedOf(row).mergeInto;
        if (appliedOf(row).option === MERGE_INTO) {
            const applied = appliedOf(state.proposal.requirements.find(other => !other.isBlocked && other.requirement.reqId === target));
            if (!applied?.ticked) return `${row.requirement.reqId} is merged into ${target}, which is not imported.`;
            if (applied.option === MERGE_INTO) return `${row.requirement.reqId} is merged into ${target}, which is merged too.`;
        }
    }
    return null;
}

/**
 * Names the package that owns the requirements.
 * @param {object} proposal The proposal.
 * @returns {string} The qualified name of the package, with "(new)" when the import creates it.
 */
function packageName(proposal) {
    if (!proposal.newPackage) return proposal.ownerName ?? "?";
    return (proposal.ownerName ? proposal.ownerName + "::" : "") + proposal.newPackage + " (new)";
}

/**
 * Gets all the rows, in the order of the table: the order of the document, or the sort chosen by the user.
 * @returns {{row: object, index: number}[]} The rows, each with its position in the proposal.
 */
function rowsInOrder() {
    const rows = state.proposal.requirements.map((row, index) => ({ row, index }));
    if (!state.sort) return rows;
    const key = SORT_KEYS[state.sort.column];
    const direction = state.sort.descending ? -1 : 1;
    return rows.sort((first, second) => direction * key(first.row).localeCompare(key(second.row), "en", { numeric: true, sensitivity: "base" }));
}

/**
 * Gets the rows that the filter shows, in the order of the table.
 * @returns {{row: object, index: number}[]} The rows, each with its position in the proposal.
 */
function shownRows() {
    return rowsInOrder().filter(({ row }) => state.filter === "all" || (state.filter === "ready") === row.isReady);
}

/**
 * Chooses the row to select among rows: the first one to review, otherwise the first one with a problem.
 * @param {{row: object, index: number}[]} rows The rows.
 * @returns {number} The position of the row in the proposal, or null when there is none.
 */
function firstToShow(rows) {
    return (rows.find(({ row }) => isToReview(row)) ?? rows.find(({ row }) => !row.isReady))?.index ?? null;
}

/**
 * Draws the whole view from the state.
 */
function render() {
    const shown = shownRows();
    if (!shown.some(({ index }) => index === state.selected)) state.selected = null;
    drawHeader();
    drawRows(shown);
    drawDetail();
    drawFooter();
}

/**
 * Draws the header: the document, the section and the counts, the filters, and the identifiers that the proposal misses.
 */
function drawHeader() {
    const proposal = state.proposal;
    const rows = proposal.requirements;
    const attention = rows.filter(row => !row.isReady).length;
    $("title").textContent = "Extract requirements · " + proposal.fileName;
    $("subtitle").textContent = [
        "§ " + proposal.sectionTitle,
        plural(rows.length, "requirement") + " found",
        attention === 1 ? "1 needs your attention" : `${attention} need your attention`,
        "target package: " + packageName(proposal)
    ].join(" · ");

    $("filters").hidden = false;
    $("count_all").textContent = String(rows.length);
    $("count_ready").textContent = String(rows.length - attention);
    $("count_attention").textContent = String(attention);
    for (const chip of $("filters").querySelectorAll("button")) {
        const on = chip.dataset.filter === state.filter;
        chip.classList.toggle("on", on);
        chip.setAttribute("aria-pressed", String(on));
    }

    const missed = proposal.missedIds ?? [];
    $("missed").hidden = missed.length === 0;
    $("missed").textContent = `The section also defines ${missed.join(", ")}, which the list misses: ask the assistant to propose ${missed.length === 1 ? "it" : "them"}.`;
}

/**
 * Draws the rows of the table that the filter shows, the card under the selected row when the view is narrow, and the sort.
 * @param {{row: object, index: number}[]} shown The rows to draw.
 */
function drawRows(shown) {
    $("body").hidden = false;
    const rows = shown.flatMap(({ row, index }) => state.narrow && state.selected === index ? [createRow(row, index), createFold()] : [createRow(row, index)]);
    if (rows.length === 0) {
        const empty = el("td", "empty", "No requirement here.");
        empty.setAttribute("colspan", String(state.columns));
        const row = el("tr");
        row.append(empty);
        rows.push(row);
    }
    $("rows").replaceChildren(...rows);

    for (const button of $("head").querySelectorAll("button[data-sort]")) {
        button.parentElement.setAttribute("aria-sort", sortStateOf(button.dataset.sort));
    }
}

/**
 * Tells how the table is sorted on a column.
 * @param {string} column The column.
 * @returns {string} The value of aria-sort: ascending, descending or none.
 */
function sortStateOf(column) {
    if (state.sort?.column !== column) return "none";
    return state.sort.descending ? "descending" : "ascending";
}

/**
 * Creates the row of a requirement: its tick, identifier, statement, kind, constraint and quality.
 * @param {object} row The row of the proposal.
 * @param {number} index The position of the row in the proposal.
 * @returns {HTMLTableRowElement} The row of the table.
 */
function createRow(row, index) {
    const requirement = row.requirement;
    const tr = el("tr", "rx-row" + (row.isReady ? "" : " need") + (state.selected === index ? " sel" : ""));
    tr.dataset.index = String(index);

    const tick = el("input", "cb");
    tick.type = "checkbox";
    tick.checked = Boolean(appliedOf(row)?.ticked);
    tick.setAttribute("aria-label", "Import " + requirement.reqId);
    tick.disabled = row.isBlocked || state.importedText !== null;

    tr.append(
        cell(tick, "rx-tick"),
        cell(el("span", "rx-id", requirement.reqId)),
        cell(requirement.text),
        cell(requirement.kind ?? "", "rx-kind"),
        cell(constraintTag(row), "rx-cst"),
        cell(qualityTag(row), "rx-q"));
    return tr;
}

/**
 * Creates the row that holds the card under the selected row, when the view is narrow.
 * @returns {HTMLTableRowElement} The row of the table.
 */
function createFold() {
    const td = el("td");
    td.setAttribute("colspan", String(state.columns));
    td.append(detailCard);
    const tr = el("tr", "rx-fold");
    tr.append(td);
    return tr;
}

/**
 * Creates a cell of the table.
 * @param {HTMLElement|string} content The element or the text of the cell.
 * @param {string} [className] The CSS classes, or nothing.
 * @returns {HTMLTableCellElement} The cell.
 */
function cell(content, className) {
    const td = el("td", className);
    td.append(content);
    return td;
}

/**
 * Creates the tag of the column "Verifiable as" for a row.
 * @param {object} row The row.
 * @returns {HTMLElement} The tag.
 */
function constraintTag(row) {
    return el("span", constraintOf(row) ? "cst" : "cst none", constraintLabel(row));
}

/**
 * Names the quality of a row: Ready, or the finding that matters most.
 * @param {object} row The row.
 * @returns {string} The name.
 */
function qualityLabel(row) {
    if (row.isReady) return "Ready";
    const finding = row.findings.find(item => BLOCKING.has(item.kind)) ?? row.findings[0];
    return FINDING_LABELS[finding.kind] ?? finding.kind;
}

/**
 * Creates the quality tag of a row.
 * @param {object} row The row.
 * @returns {HTMLElement} The tag.
 */
function qualityTag(row) {
    const tag = el("span", row.isReady ? "qtag ok" : "qtag warn");
    tag.append(icon(row.isReady ? "check" : "warn", 11), qualityLabel(row));
    return tag;
}

/**
 * Draws the card of the selected row, which has a problem: the passage of the document with the statement marked, why it needs
 * attention, and the choices for a requirement to review. Without a selected row, the table takes the whole width.
 */
function drawDetail() {
    const row = state.selected === null ? null : state.proposal.requirements[state.selected];
    detailCard.hidden = !row;
    $("body").classList.toggle("solo", !row);
    if (!state.narrow && detailCard.parentElement !== $("body")) $("body").append(detailCard);
    if (!row) return;

    const why = el("div", "why");
    why.append(el("b", null, "Why:"), " " + row.findings.map(finding => finding.message).join(" "));
    const parts = [detailHeader(row), facts(row), excerptOf(row), why];
    if (isToReview(row) && state.importedText === null) {
        const group = el("div", "opt2");
        group.setAttribute("role", "radiogroup");
        group.setAttribute("aria-label", "How to import " + row.requirement.reqId);
        group.append(...optionsOf(row));
        parts.push(el("div", "sec", "What should Hypha do?"), group, navigation(row));
    }
    detailCard.replaceChildren(...parts);
}

/**
 * Creates the header of the card.
 * @param {object} row The row.
 * @returns {HTMLElement} The header.
 */
function detailHeader(row) {
    const end = el("span", "r");
    end.append(qualityTag(row));
    const header = el("div", "card-h");
    header.append(icon("warn", 15), row.requirement.reqId, end);
    return header;
}

/**
 * Creates the lines that give the kind and the constraint of a row, shown in the card when the table has no room for them.
 * @param {object} row The row.
 * @returns {HTMLElement} The lines.
 */
function facts(row) {
    const box = el("div", "rx-facts");
    box.append(el("span", null, "Kind: "), (row.requirement.kind ?? "—") + " · ", el("span", null, "Verifiable as: "), constraintTag(row));
    return box;
}

/**
 * Creates the passage of the document that holds the statement of a row, the statement marked.
 * @param {object} row The row.
 * @returns {HTMLElement} The passage.
 */
function excerptOf(row) {
    const box = el("div", "excerpt");
    const statement = normalize(row.requirement.text);
    const start = row.excerpt ? row.excerpt.indexOf(statement) : -1;
    if (start < 0) {
        box.textContent = row.excerpt ?? row.requirement.text;
        return box;
    }
    box.append(row.excerpt.slice(0, start), el("mark", null, statement), row.excerpt.slice(start + statement.length));
    return box;
}

/**
 * Gets what is picked in the card of a row: the last pick, otherwise what is applied, otherwise the rewrite if the server accepts it.
 * @param {object} row The row.
 * @returns {{option: string, mergeInto: string}} The pick; its option is undefined when nothing is picked.
 */
function pickOf(row) {
    return state.picks.get(row.requirement.reqId) ?? appliedOf(row) ?? { option: hasValidRewrite(row) ? WITH_REWRITE : undefined };
}

/**
 * Creates the choices of a requirement to review.
 * @param {object} row The row.
 * @returns {HTMLElement[]} The choices.
 */
function optionsOf(row) {
    const requirement = row.requirement;
    const options = [];
    if (hasValidRewrite(row)) {
        options.push(option(row, WITH_REWRITE, requirement.rewrittenConstraint ? "Rewrite with a threshold" : "Rewrite", `“${requirement.rewrittenText}”`, requirement.rewrittenConstraint));
    }
    options.push(option(row, AS_GOAL, "Keep as a design goal", "Imported without a constraint: it is not checked."));
    if (mergeCandidates(row).length > 0) options.push(mergeOption(row));
    options.push(option(row, LEAVE_OUT, "Leave out", "Not imported now."));
    return options;
}

/**
 * Creates one choice of a requirement to review.
 * @param {object} row The row.
 * @param {string} value The option of the choice.
 * @param {string} title The title of the choice.
 * @param {string} detail What the choice does.
 * @param {object} [constraint] The constraint that the requirement gets, or nothing.
 * @param {HTMLSelectElement} [list] The list that follows the title of the choice, or nothing.
 * @returns {HTMLElement} The choice.
 */
function option(row, value, title, detail, constraint, list) {
    const on = pickOf(row).option === value;
    const box = el("div", "oc" + (on ? " on" : ""));
    box.setAttribute("role", "radio");
    box.setAttribute("aria-checked", String(on));
    box.setAttribute("tabindex", "0");
    const text = el("div");
    text.append(el("b", null, title));
    if (list) text.append(list);
    text.append(el("small", null, detail));
    if (constraint) {
        const tag = el("div", "m");
        tag.append(el("span", null, constraintText(constraint)));
        text.append(tag);
    }
    box.append(el("span", "r"), text);
    const pickIt = () => pick(row, { option: value, mergeInto: list?.value }, box);
    box.addEventListener("click", pickIt);
    box.addEventListener("keydown", event => {
        if (event.target === box && (event.key === "Enter" || event.key === " ")) {
            event.preventDefault();
            pickIt();
        }
    });
    return box;
}

/**
 * Creates the choice that merges a requirement into another row, with the list of the rows: the target applied or picked, otherwise
 * the one that the assistant proposes.
 * @param {object} row The row.
 * @returns {HTMLElement} The choice.
 */
function mergeOption(row) {
    const candidates = mergeCandidates(row).map(candidate => candidate.requirement.reqId);
    const wanted = [pickOf(row).mergeInto, row.requirement.mergeInto].find(reqId => candidates.includes(reqId));
    const select = el("select");
    select.setAttribute("aria-label", "The requirement that receives " + row.requirement.reqId);
    select.append(...candidates.map(reqId => {
        const item = el("option", null, reqId);
        item.value = reqId;
        return item;
    }));
    select.value = wanted ?? candidates[0];
    const box = option(row, MERGE_INTO, "Merge into", "Dropped as a separate requirement.", null, select);
    select.addEventListener("change", () => pick(row, { option: MERGE_INTO, mergeInto: select.value }, box));
    return box;
}

/**
 * Keeps what is picked in the card of a row, without drawing the card again: an open list stays open.
 * @param {object} row The row.
 * @param {{option: string, mergeInto: string}} picked The pick.
 * @param {HTMLElement} box The choice picked.
 */
function pick(row, picked, box) {
    state.picks.set(row.requirement.reqId, picked);
    for (const item of box.parentElement.querySelectorAll(".oc")) {
        item.classList.toggle("on", item === box);
        item.setAttribute("aria-checked", String(item === box));
    }
    $("apply").disabled = false;
}

/**
 * Creates the buttons at the bottom of the card, which go through the requirements to review.
 * @param {object} row The row.
 * @returns {HTMLElement} The buttons.
 */
function navigation(row) {
    const order = rowsInOrder().filter(item => isToReview(item.row)).map(item => item.index);
    const at = order.indexOf(state.selected);
    const previous = el("button", "btn sm");
    previous.type = "button";
    previous.append(icon("previous", 12), "Previous");
    previous.disabled = at === 0;
    previous.addEventListener("click", () => goTo(order[at - 1]));

    const next = order[at + 1];
    const apply = el("button", "btn pri sm");
    apply.type = "button";
    apply.setAttribute("id", "apply");
    apply.append(next === undefined ? "Apply" : "Apply and next", icon("next", 12));
    apply.disabled = pickOf(row).option === undefined;
    apply.addEventListener("click", () => {
        applyPick(row);
        goTo(next ?? state.selected);
    });

    const bar = el("div", "foot-bar");
    bar.append(previous, el("span", "sp"), apply);
    return bar;
}

/**
 * Applies what is picked in the card of a row: the row is ticked, except when it is left out.
 * @param {object} row The row.
 */
function applyPick(row) {
    const picked = pickOf(row);
    if (picked.option === LEAVE_OUT) {
        state.applied.delete(row.requirement.reqId);
        return;
    }
    const mergeInto = picked.option === MERGE_INTO ? picked.mergeInto : undefined;
    state.applied.set(row.requirement.reqId, { option: picked.option, mergeInto, ticked: true });
}

/**
 * Selects a row and shows it in the table.
 * @param {number} index The position of the row in the proposal.
 */
function goTo(index) {
    state.selected = index;
    render();
    $("rows").querySelector(`tr[data-index="${index}"]`)?.scrollIntoView({ block: "nearest" });
}

/**
 * Draws the footer: what the import creates, the reason why it cannot be done, and its buttons; or what has been imported.
 */
function drawFooter() {
    const done = state.importedText !== null;
    const chosen = chosenRows();
    const merged = chosen.filter(row => appliedOf(row).option === MERGE_INTO).length;
    const problem = mergeProblem();

    $("summary").className = done ? "done" : "";
    if (done) $("summary").replaceChildren(icon("check", 16), state.importedText);
    else $("summary").replaceChildren(...summaryOf(chosen, merged));

    $("reason").hidden = !problem;
    $("reason").textContent = problem ?? "";
    $("import").hidden = done;
    $("export").hidden = done;
    $("import").disabled = chosen.length === merged || problem !== null || state.busy;
    $("export").disabled = state.busy;
    drawTickAll(chosen.length, done);
}

/**
 * Writes what the import creates.
 * @param {object[]} chosen The rows that the user imports.
 * @param {number} merged How many of them are merged.
 * @returns {(HTMLElement|string)[]} The parts of the text.
 */
function summaryOf(chosen, merged) {
    if (chosen.length === 0) return ["Nothing selected"];
    const constraints = chosen.filter(row => constraintOf(row) !== null).length;
    const creates = ` · creates ${plural(chosen.length - merged, "requirement")} and ${plural(constraints, "constraint")}`;
    return [el("b", null, `${chosen.length} selected`), creates + (merged === 0 ? "" : ` · ${merged} merged`)];
}

/**
 * Draws the box that ticks or unticks all the rows that have something applied.
 * @param {number} ticked How many rows are ticked.
 * @param {boolean} done true once the requirements are imported.
 */
function drawTickAll(ticked, done) {
    const count = tickableRows().length;
    const box = $("all");
    box.hidden = done;
    box.disabled = count === 0;
    box.checked = ticked > 0 && ticked === count;
    box.indeterminate = ticked > 0 && ticked < count;
}

/**
 * Gets the rows that a tick can import: those that have something applied.
 * @returns {object[]} The rows.
 */
function tickableRows() {
    return state.proposal.requirements.filter(row => appliedOf(row) !== null);
}

/**
 * Ticks all the rows that have something applied, or unticks them when they are all ticked.
 */
function tickAll() {
    const rows = tickableRows();
    const on = !rows.every(row => appliedOf(row).ticked);
    for (const row of rows) appliedOf(row).ticked = on;
    render();
}

/**
 * Ticks or unticks a row that has something applied; a row without choice is selected instead, so that the user chooses.
 * @param {number} index The position of the row in the proposal.
 */
function toggle(index) {
    const row = state.proposal.requirements[index];
    if (row.isBlocked || state.importedText !== null) return;
    const applied = appliedOf(row);
    if (applied) {
        applied.ticked = !applied.ticked;
        render();
    } else {
        state.selected = index;
        render();
    }
}

/**
 * Selects a row that has a problem, which opens its card. Selecting a ready row, or the same row again, closes the card.
 * @param {number} index The position of the row in the proposal.
 */
function select(index) {
    state.selected = state.proposal.requirements[index].isReady || state.selected === index ? null : index;
    render();
}

/**
 * Filters the table. With room for the card, it opens on the first row to review when the selected row is no longer shown.
 * @param {string} filter The filter: all, ready or attention.
 */
function setFilter(filter) {
    state.filter = filter;
    const shown = shownRows();
    if (!state.narrow && !shown.some(({ index }) => index === state.selected)) state.selected = firstToShow(shown);
    render();
}

/**
 * Sorts the table on a column: ascending, then descending, then back to the order of the document.
 * @param {string} column The column.
 */
function sortOn(column) {
    if (state.sort?.column !== column) state.sort = { column, descending: false };
    else state.sort = state.sort.descending ? null : { column, descending: true };
    render();
}

/**
 * Calls a tool of the server through the host, with the proposal.
 * @param {string} name The name of the tool.
 * @param {object} args The other arguments of the tool.
 * @param {string} progress What the view says during the call.
 * @param {function(object): (Promise<void>|void)} onResult Called with the result of the tool.
 * @returns {Promise<void>} Resolved once the view shows the result or the error.
 */
async function callTool(name, args, progress, onResult) {
    state.busy = true;
    showError(null);
    $("note").textContent = progress;
    drawFooter();
    try {
        const result = await request("tools/call", { name, arguments: { proposalId: state.proposal.id, token: state.proposal.token, ...args } });
        if (result?.isError) throw new Error(result.content?.[0]?.text || name + " has failed.");
        await onResult(result);
    } catch (error) {
        showError(error.message);
        $("note").textContent = "";
    } finally {
        state.busy = false;
        drawFooter();
    }
}

/**
 * Keeps the list and the choices in the memory of the server, without importing anything.
 * @returns {Promise<void>} Resolved once the view shows the result or the error.
 */
function saveList() {
    const rows = state.proposal.requirements.filter(row => !row.isBlocked).map(row => {
        const applied = appliedOf(row);
        return { reqId: row.requirement.reqId, option: applied?.option, mergeInto: applied?.mergeInto, ticked: applied?.ticked ?? false };
    });
    return callTool(SAVE_TOOL, { rows }, "Saving…", () => {
        $("note").textContent = "List saved, nothing imported: propose this section again to get your choices back. It is lost if the server restarts.";
    });
}

/**
 * Imports the chosen requirements through the host, without the AI assistant, then tells the assistant what has been imported.
 * @returns {Promise<void>} Resolved once the view shows the result or the error.
 */
function importChosen() {
    const choices = chosenRows().map(row => {
        const applied = appliedOf(row);
        return { reqId: row.requirement.reqId, option: applied.option, ...(applied.mergeInto ? { mergeInto: applied.mergeInto } : {}) };
    });
    return callTool(IMPORT_TOOL, { choices }, "Importing…", result => showImport(result.structuredContent, choices));
}

/**
 * Shows the result of the import: what has been created and the message for the assistant, or why the batch has been refused.
 * @param {object} result The result of import_requirements.
 * @param {object[]} choices The choices sent.
 * @returns {Promise<void>} Resolved once the assistant has the message.
 */
async function showImport(result, choices) {
    if (!result?.imported) {
        throw new Error("Nothing has been imported: " + (result?.result?.problems ?? []).join(" "));
    }
    state.importedText = "Imported in one commit: " + choices.filter(choice => choice.option !== MERGE_INTO).map(choice => choice.reqId).join(", ") + ".";
    render();
    $("note").textContent = await tellAssistant(result.message);
}

/**
 * Gives a message to the assistant: in the chat box when the host allows it, otherwise as context of the conversation.
 * @param {string} text The message.
 * @returns {Promise<string>} What the user does next.
 */
async function tellAssistant(text) {
    const content = [{ type: "text", text }];
    const result = await request("ui/message", { role: "user", content }).catch(() => ({ isError: true }));
    if (!result?.isError) return "The message is in the chat box: check it and press Enter.";
    return request("ui/update-model-context", { content })
        .then(() => "Sent to the assistant: type \"go\" in the chat.")
        .catch(() => "The host refused the message: tell the assistant what you have imported.");
}

/**
 * Follows the size of the view: tells the host, and draws the view again when the card changes place.
 */
function onResize() {
    const narrow = window.innerWidth < WIDE_VIEW;
    const columns = visibleColumns();
    const moved = narrow !== state.narrow || columns !== state.columns;
    state.narrow = narrow;
    state.columns = columns;
    if (moved && state.proposal) render();
    sendSize();
}

// The events of the view: the filters, the sort, the rows (tick or select), the footer, the link to the conversation, the size of the page.
$("filters").addEventListener("click", event => {
    const chip = event.target.closest("button[data-filter]");
    if (chip && state.proposal) setFilter(chip.dataset.filter);
});
$("head").addEventListener("click", event => {
    const button = event.target.closest("button[data-sort]");
    if (button && state.proposal) sortOn(button.dataset.sort);
});
$("rows").addEventListener("click", event => {
    const tr = event.target.closest("tr[data-index]");
    if (!tr) return;
    const index = Number(tr.dataset.index);
    if (event.target.closest(".cb")) toggle(index);
    else select(index);
});
$("all").addEventListener("click", tickAll);
$("export").addEventListener("click", () => {
    void saveList();
});
$("import").addEventListener("click", () => {
    void importChosen();
});
$("back").replaceChildren(icon("previous", 13), "Hypha conversation");
$("back").addEventListener("click", () => display.ask("inline"));
$("import").replaceChildren(icon("check", 14), "Import as 1 batch");
listenToHost(onToolResult, display.onHostContext);
new ResizeObserver(onResize).observe(document.body);
start();
