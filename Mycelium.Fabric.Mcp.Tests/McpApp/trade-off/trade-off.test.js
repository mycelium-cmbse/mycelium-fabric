// ------------------------------------------------------------------------------------------------
//  <copyright file="trade-off.test.js" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

// Tests of the view "Trade-off" (McpApp/trade-off/trade-off.js), run by Node.js: node --test Mycelium.Fabric.Mcp.Tests/McpApp/trade-off/trade-off.test.js

const { describe, it, beforeEach } = require("node:test");
const assert = require("node:assert/strict");
const path = require("node:path");

const { loadPage } = require("../fake-page.js");

const APP_FOLDER = path.join(__dirname, "..", "..", "..", "Mycelium.Fabric.Mcp", "McpApp");
const HOST_ORIGIN = "https://host.example";
const ROOT_ID = "5d06ef4d-4489-500c-e94f-a8cf563d3fc1";
const CAMERA = { partId: "558a0ae4-8585-66a3-9bc6-52322650941c", qualifiedName: "EOSat1::Architecture::eosat1::payloadSubsystem::camera" };
const TRANSMITTER = { partId: "324f00ec-273e-5fe1-c724-a63af46fee09", qualifiedName: "EOSat1::Architecture::eosat1::communicationSubsystem::xBandTransmitter" };

/**
 * Builds the variants taken by a combination of EOSat-1.
 * @param {string} camera The variant of the camera.
 * @param {string} transmitter The variant of the X-band transmitter.
 * @param {number} [transmitters] The number of transmitters.
 * @returns {object[]} The variant of each choice, as the server gives it for a combination.
 */
function chosen(camera, transmitter, transmitters = 1) {
    return [
        { choice: "camera", ...CAMERA, variant: camera, quantity: 1, changed: camera !== "current" },
        { choice: "xBandTransmitter", ...TRANSMITTER, variant: transmitter, quantity: transmitters, changed: transmitter !== "current" }
    ];
}

/**
 * Builds a combination of EOSat-1, with its totals and the check of REQ-SYS-001.
 * @param {number} rank The rank of the combination, 0 for today.
 * @param {string} camera The variant of the camera.
 * @param {string} transmitter The variant of the X-band transmitter.
 * @param {number[]} totals The mass, the power, and their difference from today.
 * @param {number} value The mass with its margin, compared to 150 kg.
 * @returns {object} The combination.
 */
function combination(rank, camera, transmitter, [mass, power, massDifference, powerDifference], value) {
    return {
        alternative: null,
        rank,
        changeCount: chosen(camera, transmitter).filter(choice => choice.changed).length,
        choices: chosen(camera, transmitter),
        totals: [
            { attribute: "mass", total: mass, difference: massDifference, unit: "kg" },
            { attribute: "power", total: power, difference: powerDifference, unit: null }
        ],
        constraints: [{ constraint: "mass * 1.2 <= 150 kg", satisfied: value <= 150, name: "REQ-SYS-001", value, limit: 150 }]
    };
}

/**
 * Builds a point of the chart of EOSat-1.
 * @param {string} camera The variant of the camera.
 * @param {string} transmitter The variant of the X-band transmitter.
 * @param {number} mass The total mass.
 * @param {number} power The total power.
 * @param {string[]} [broken] Why the combination is excluded, or nothing.
 * @returns {object} The point.
 */
function point(camera, transmitter, mass, power, broken = []) {
    return {
        choices: [{ choice: "camera", variant: camera, quantity: 1 }, { choice: "xBandTransmitter", variant: transmitter, quantity: 1 }],
        totals: { mass, power },
        feasible: broken.length === 0,
        rank: null,
        isCurrent: camera === "current" && transmitter === "current",
        broken
    };
}

/**
 * Builds the study of the camera and the X-band transmitter of EOSat-1, as solve_trade_off gives it to the view (real values
 * of the server), unless told otherwise.
 * @param {object} [values] The values that replace those of the study.
 * @returns {object} The study.
 */
function study(values) {
    const objectives = [{ attribute: "mass", direction: "Minimize" }, { attribute: "power", direction: "Minimize" }];

    return {
        title: "Camera and X-band transmitter",
        root: { id: ROOT_ID, qualifiedName: "EOSat1::Architecture::eosat1" },
        status: "Optimal",
        request: {
            rootId: ROOT_ID,
            choices: [
                { partId: CAMERA.partId, name: "camera", variants: [{ name: "A", values: { mass: 30, power: 70 } }, { name: "B", values: { mass: 34, power: 50 } }] },
                { partId: TRANSMITTER.partId, name: "xBandTransmitter", variants: [{ name: "XT-low", values: { mass: 1.2, power: 28 } }] }
            ],
            objectives,
            constraints: [{ name: "REQ-SYS-001", attribute: "mass", operator: "<=", limit: 150, unit: "kg", limitAttribute: null, margin: 20 }],
            rules: null,
            count: 3,
            compareBy: null,
            excluded: [],
            title: "Camera and X-band transmitter"
        },
        attributes: [{ name: "mass", unit: "kg" }, { name: "power", unit: null }],
        choices: [
            {
                name: "camera",
                ...CAMERA,
                variants: [
                    { name: "current", isCurrent: true, excluded: false, values: { mass: 38, power: 55 } },
                    { name: "A", isCurrent: false, excluded: false, values: { mass: 30, power: 70 } },
                    { name: "B", isCurrent: false, excluded: false, values: { mass: 34, power: 50 } }
                ]
            },
            {
                name: "xBandTransmitter",
                ...TRANSMITTER,
                variants: [
                    { name: "current", isCurrent: true, excluded: false, values: { mass: 1.6, power: 35 } },
                    { name: "XT-low", isCurrent: false, excluded: false, values: { mass: 1.2, power: 28 } }
                ]
            }
        ],
        objectives,
        constraints: [{ name: "REQ-SYS-001", description: "mass * 1.2 <= 150 kg", attribute: "mass", operator: "<=", limit: 150, limitAttribute: null, margin: 20, threshold: 125 }],
        rules: [],
        current: combination(0, "current", "current", [125.8, 227, 0, 0], 150.96),
        best: [
            combination(1, "A", "XT-low", [117.4, 235, -8.4, 8], 140.88),
            combination(2, "A", "current", [117.8, 242, -8, 15], 141.36),
            combination(3, "B", "XT-low", [121.4, 215, -4.4, -12], 145.68)
        ],
        points: [
            point("current", "current", 125.8, 227, ["REQ-SYS-001: mass * 1.2 = 150.96 kg > 150 kg"]),
            point("current", "XT-low", 125.4, 220, ["REQ-SYS-001: mass * 1.2 = 150.48 kg > 150 kg"]),
            point("A", "current", 117.8, 242),
            point("A", "XT-low", 117.4, 235),
            point("B", "current", 121.8, 222),
            point("B", "XT-low", 121.4, 215)
        ],
        pointsComplete: true,
        conflicts: [],
        ...values
    };
}

/**
 * Builds the notification of the host that gives the result of solve_trade_off.
 * @param {object} structuredContent The study, or nothing.
 * @param {string} [text] The text of the result, or nothing.
 * @returns {object} The notification.
 */
function toolResult(structuredContent, text) {
    const content = text === undefined ? undefined : [{ type: "text", text }];
    return { jsonrpc: "2.0", method: "ui/notifications/tool-result", params: { content, structuredContent } };
}

/**
 * Lets the pending promises of the view go on.
 * @returns {Promise<void>} Resolved once they have run.
 */
function flush() {
    return new Promise(resolve => setImmediate(resolve));
}

/**
 * Gets an element of the page.
 * @param {object} page The page.
 * @param {string} id The id of the element.
 * @returns {object} The element.
 */
function byId(page, id) {
    return page.document.getElementById(id);
}

/**
 * Gets the messages that the view has sent to the host.
 * @param {object} page The page.
 * @returns {object[]} The messages.
 */
function messages(page) {
    return page.sent.map(entry => entry.message);
}

/**
 * Gets the requests that the view has sent to the host.
 * @param {object} page The page.
 * @param {string} method The method of the requests.
 * @returns {object[]} The requests.
 */
function requests(page, method) {
    return messages(page).filter(message => message.method === method && message.id !== undefined);
}

/**
 * Answers a request of the view, then lets the view go on.
 * @param {object} page The page.
 * @param {object} message The request.
 * @param {object} result The result, or nothing when the host answers with an error.
 * @param {object} [error] The JSON-RPC error.
 */
async function answer(page, message, result, error) {
    page.receive(error ? { jsonrpc: "2.0", id: message.id, error } : { jsonrpc: "2.0", id: message.id, result });
    await flush();
}

/**
 * Answers the last call of explore_trade_off with a study.
 * @param {object} page The page.
 * @param {object} newStudy The study.
 */
async function answerStudy(page, newStudy) {
    await answer(page, requests(page, "tools/call").at(-1), { content: [{ type: "text", text: "" }], structuredContent: newStudy });
}

/**
 * Reads the best options of the list.
 * @param {object} page The page.
 * @returns {string[]} For each option: its number, its rank class, whether it is selected, its variants, totals and checks.
 */
function options(page) {
    return byId(page, "options").querySelectorAll(".tr-option").map(card => [
        card.querySelector(".tr-rank").textContent + (card.classList.contains("on") ? "*" : "") + " (" + card.querySelector(".tr-rank").className + ")",
        card.querySelector(".tr-name").textContent,
        card.querySelector(".tr-totals").textContent,
        card.querySelector(".tr-checks").textContent
    ].join(" | "));
}

/**
 * Reads the points of the chart.
 * @param {object} page The page.
 * @returns {string[]} For each point, in the order drawn: its class, its number and its tooltip.
 */
function points(page) {
    return byId(page, "chart").querySelectorAll("g").map(group => {
        const number = group.children.find(child => child.tagName === "TEXT")?.textContent ?? "-";
        return group.className + " | " + number + " | " + group.querySelector("title").textContent;
    });
}

/**
 * Gets a field of the set-up.
 * @param {object} page The page.
 * @param {string} kind The kind of the field: variant, enabled, limit or margin.
 * @param {string} key The key of the field: choice.variant, or the index of the constraint.
 * @returns {object} The field.
 */
function field(page, kind, key) {
    return byId(page, "setup").querySelectorAll("input").find(input => input.dataset.kind === kind && String(input.dataset.key) === key);
}

/**
 * Changes a field of the set-up, as the user does.
 * @param {object} field The field.
 * @param {object} values The new values of the field, for example checked or value.
 */
function change(field, values) {
    Object.assign(field, values);
    field.dispatch("change");
}

/**
 * Gets the lines of the objectives.
 * @param {object} page The page.
 * @returns {object[]} The lines, in their order.
 */
function objectiveLines(page) {
    return byId(page, "objectives").querySelectorAll(".tr-objective");
}

/**
 * Gives the lines of the objectives their place on the screen: 30 pixels high each, from 100 pixels.
 * @param {object} page The page.
 */
function placeObjectives(page) {
    objectiveLines(page).forEach((line, index) => {
        line.getBoundingClientRect = () => ({ top: 100 + 30 * index, height: 30 });
    });
}

/**
 * Gets the arguments of the last call of explore_trade_off.
 * @param {object} page The page.
 * @returns {object} The arguments.
 */
function lastArguments(page) {
    return requests(page, "tools/call").at(-1).params.arguments;
}

describe("trade-off view", () => {
    let page;

    /**
     * Initializes the view, then shows a study as the result of solve_trade_off.
     * @param {object} first The study.
     */
    async function open(first) {
        await answer(page, messages(page)[0], { hostContext: { theme: "light" } });
        page.receive(toolResult(first));
    }

    beforeEach(() => {
        page = loadPage(path.join(APP_FOLDER, "trade-off", "trade-off.html"), path.join(APP_FOLDER, "host.js"), path.join(APP_FOLDER, "trade-off", "trade-off.js"));
    });

    describe("talking with the host", () => {
        it("initializes with the parent frame, whatever its origin", () => {
            assert.deepEqual(page.sent, [{
                message: {
                    jsonrpc: "2.0",
                    id: 1,
                    method: "ui/initialize",
                    params: { protocolVersion: "2026-01-26", appInfo: { name: "trade-off", version: "0.1.0" }, appCapabilities: {} }
                },
                targetOrigin: "*"
            }]);
        });

        it("applies the theme of the host, then sends the next messages to its origin only", async () => {
            await answer(page, messages(page)[0], { hostContext: { theme: "dark", styles: { variables: { "--color-background-primary": "#262624", "--font-sans": "" } } } });

            const style = page.document.documentElement.style;
            assert.equal(style["--color-background-primary"], "#262624");
            assert.equal(style["--font-sans"], undefined);
            assert.equal(style.colorScheme, "dark");
            assert.deepEqual(page.sent.at(-1), { message: { jsonrpc: "2.0", method: "ui/notifications/initialized", params: {} }, targetOrigin: HOST_ORIGIN });
        });

        it("tells why the host refused to start the view", async () => {
            await answer(page, messages(page)[0], null, { code: -32603, message: "Not ready" });

            assert.deepEqual([byId(page, "error").hidden, byId(page, "error").textContent], [false, "Not ready"]);
        });

        it("tells that the host refused to start the view, even without a reason", async () => {
            await answer(page, messages(page)[0], null, { code: -32603 });

            assert.equal(byId(page, "error").textContent, "The host did not start the view.");
        });

        it("follows the changes of theme, and answers the requests of the host", async () => {
            await answer(page, messages(page)[0], {});
            page.receive({ jsonrpc: "2.0", method: "ui/notifications/host-context-changed", params: { theme: "light", styles: { variables: { "--color-text-primary": "#1f2937" } } } });
            page.receive({ jsonrpc: "2.0", id: 9, method: "ping" });
            page.receive({ jsonrpc: "2.0", method: "ui/notifications/host-context-changed" });

            const style = page.document.documentElement.style;
            assert.equal(style.colorScheme, "light");
            assert.equal(style["--color-text-primary"], "#1f2937");
            assert.deepEqual(page.sent.at(-1), { message: { jsonrpc: "2.0", id: 9, result: {} }, targetOrigin: HOST_ORIGIN });
        });

        it("ignores the messages of another frame, of another origin, that are not JSON-RPC, or that answer nothing", async () => {
            await answer(page, messages(page)[0], {});
            const count = page.sent.length;

            page.receive({ jsonrpc: "2.0", id: 7, method: "ping" }, HOST_ORIGIN, {});
            page.receive({ jsonrpc: "2.0", id: 7, method: "ping" }, "https://other.example");
            page.receive({ id: 7, method: "ping" });
            page.receive(null);
            page.receive({ jsonrpc: "2.0", id: 42, result: {} });

            assert.equal(page.sent.length, count);
            assert.equal(byId(page, "title").textContent, "Waiting for the trade-off…");
        });

        it("keeps sending to any origin when the frame of the host has none", () => {
            page.receive({ jsonrpc: "2.0", id: 9, method: "ping" }, "null");

            assert.equal(page.sent.at(-1).targetOrigin, "*");
        });

        it("tells the host the size of the view", () => {
            page.document.documentElement.style.height = "auto";

            page.resize();

            assert.deepEqual(page.sent.at(-1).message, { jsonrpc: "2.0", method: "ui/notifications/size-changed", params: { width: 600, height: 413 } });
            assert.equal(page.document.documentElement.style.height, "auto");
        });
    });

    describe("showing the result of solve_trade_off", () => {
        it("shows the title, the root, the number of combinations and the status", async () => {
            await open(study());

            assert.equal(byId(page, "title").textContent, "Camera and X-band transmitter");
            assert.equal(byId(page, "subtitle").textContent, "EOSat1::Architecture::eosat1 · 6 combinations · best options found");
            assert.equal(byId(page, "grid").hidden, false);
            assert.equal(byId(page, "error").hidden, true);
        });

        it("names the trade-off after its choices without a title, and tells when the combinations are too many to show", async () => {
            await open(study({ title: null, pointsComplete: false, status: "ModelChanged" }));

            assert.equal(byId(page, "title").textContent, "Trade-off · camera, xBandTransmitter");
            assert.equal(byId(page, "subtitle").textContent, "EOSat1::Architecture::eosat1 · too many combinations to show them all · ModelChanged");
        });

        it("lists the best options from 1, with their totals against today and their checks, the first one selected", async () => {
            await open(study());

            assert.equal(byId(page, "today").textContent, "vs today: 125.8 kg · 227");
            assert.deepEqual(options(page), [
                "1* (tr-rank n1) | camera: A · xBandTransmitter: XT-low | 117.4 kg (−8.4) · 235 (+8) | ✓ REQ-SYS-001 · 140.88 / 150",
                "2 (tr-rank n2) | camera: A · xBandTransmitter: current | 117.8 kg (−8) · 242 (+15) | ✓ REQ-SYS-001 · 141.36 / 150",
                "3 (tr-rank n3) | camera: B · xBandTransmitter: XT-low | 121.4 kg (−4.4) · 215 (−12) | ✓ REQ-SYS-001 · 145.68 / 150"
            ]);
            const differences = byId(page, "options").querySelectorAll(".tr-totals span").map(span => span.className);
            assert.deepEqual(differences, ["tr-better", "tr-worse", "tr-better", "tr-worse", "tr-better", "tr-better"]);
        });

        it("colours a difference by the way of its objective, shows an unchanged total, a quantity, and a check without value", async () => {
            const option = combination(1, "A", "XT-low", [117.4, 227, -8.4, 0], 140.88);
            option.choices = chosen("A", "XT-low", 2);
            option.constraints.push({ constraint: "power <= powerOutput", satisfied: false, name: null, value: null, limit: null });

            await open(study({ objectives: [{ attribute: "mass", direction: "Maximize" }], best: [option] }));

            assert.deepEqual(options(page), [
                "1* (tr-rank n1) | camera: A · xBandTransmitter: XT-low × 2 | 117.4 kg (−8.4) · 227 (=) | ✓ REQ-SYS-001 · 140.88 / 150✗ power <= powerOutput"
            ]);
            assert.deepEqual(byId(page, "options").querySelectorAll(".tr-totals span").map(span => span.className), ["tr-worse", ""]);
            assert.deepEqual(byId(page, "options").querySelectorAll(".tr-checks span").map(span => span.className), ["tagv ok", "tagv bad"]);
        });

        it("lists the excluded combinations with the reasons", async () => {
            await open(study());

            const excluded = byId(page, "excluded");
            assert.deepEqual(excluded.querySelectorAll("h3").map(heading => heading.textContent), ["Excluded (2)"]);
            const list = excluded.querySelector(".tr-scroll");
            assert.equal(list.tabIndex, 0);
            assert.equal(list.ariaLabel, "Excluded combinations");
            assert.deepEqual(list.children.map(line => line.textContent), [
                "camera: current · xBandTransmitter: current · REQ-SYS-001: mass * 1.2 = 150.96 kg > 150 kg",
                "camera: current · xBandTransmitter: XT-low · REQ-SYS-001: mass * 1.2 = 150.48 kg > 150 kg"
            ]);
        });

        it("tells why no combination works, without option to use", async () => {
            const rejected = study().points.map(p => ({ ...p, feasible: false, broken: ["constraint 1"] }));

            await open(study({ status: "Infeasible", best: [], points: rejected, conflicts: ["constraint 1: mass * 1.2 <= 150 kg"] }));

            assert.equal(byId(page, "subtitle").textContent, "EOSat1::Architecture::eosat1 · 6 combinations · no combination meets the constraints");
            assert.equal(byId(page, "options").textContent, "No combination meets the constraints.");
            assert.deepEqual(byId(page, "excluded").querySelectorAll("h3").map(heading => heading.textContent), ["Excluded (6)", "Why no combination works"]);
            assert.equal(byId(page, "excluded").children.at(-1).textContent, "constraint 1: mass * 1.2 <= 150 kg");
            assert.equal(byId(page, "pick").hidden, true);
        });

        it("shows nothing excluded when every combination works", async () => {
            await open(study({ points: study().points.slice(2) }));

            assert.equal(byId(page, "excluded").children.length, 0);
        });

        it("tells that the trade-off could not be computed, with the error of the tool", async () => {
            await answer(page, messages(page)[0], {});

            page.receive(toolResult(undefined, "No element has the identifier 5d06ef4d."));

            assert.equal(byId(page, "title").textContent, "The trade-off could not be computed");
            assert.equal(byId(page, "grid").hidden, true);
            assert.equal(byId(page, "error").textContent, "No element has the identifier 5d06ef4d.");
            assert.equal(byId(page, "error").hidden, false);
        });

        it("tells that the host sent no trade-off", async () => {
            await answer(page, messages(page)[0], {});

            page.receive({ jsonrpc: "2.0", method: "ui/notifications/tool-result" });

            assert.equal(byId(page, "error").textContent, "The host sent no trade-off to show.");
        });
    });

    describe("drawing the chart", () => {
        it("draws every combination, the best options numbered and the selected one on top, with their tooltips", async () => {
            await open(study());

            assert.deepEqual(points(page), [
                "pt pt-bad | - | camera: current · xBandTransmitter: current · 125.8 kg · 227 · REQ-SYS-001: mass * 1.2 = 150.96 kg > 150 kg",
                "pt pt-bad | - | camera: current · xBandTransmitter: XT-low · 125.4 kg · 220 · REQ-SYS-001: mass * 1.2 = 150.48 kg > 150 kg",
                "pt pt-ok | - | camera: B · xBandTransmitter: current · 121.8 kg · 222",
                "pt pt-best pt-rank3 | 3 | Option 3 · camera: B · xBandTransmitter: XT-low · 121.4 kg · 215",
                "pt pt-best pt-rank2 | 2 | Option 2 · camera: A · xBandTransmitter: current · 117.8 kg · 242",
                "pt pt-best pt-rank1 | 1 | Option 1 · camera: A · xBandTransmitter: XT-low · 117.4 kg · 235"
            ]);
            assert.equal(byId(page, "chart").querySelectorAll(".tr-ring").length, 1);
            assert.equal(byId(page, "chart").querySelectorAll("g").at(-1).querySelectorAll(".tr-ring").length, 1);
            assert.equal(byId(page, "chart").querySelectorAll(".tr-today-dot").length, 1);
        });

        it("names the axes with their units, and marks today", async () => {
            await open(study());

            const texts = byId(page, "chart").querySelectorAll("text").map(text => text.textContent);
            assert.deepEqual(byId(page, "chart").querySelectorAll(".tr-axis").map(text => text.textContent), ["mass [kg]", "power"]);
            assert.ok(texts.includes("today"));
            assert.ok(texts.includes("120"));
            assert.ok(texts.includes("230"));
        });

        it("draws the limit of a constraint on the horizontal axis, the area beyond it forbidden, and its legend", async () => {
            await open(study());

            const chart = byId(page, "chart");
            const line = chart.querySelector(".tr-limit");
            const area = chart.querySelector(".tr-forbidden");
            assert.equal(line.getAttribute("x1"), line.getAttribute("x2"));
            assert.equal(area.getAttribute("x"), line.getAttribute("x1"));
            assert.equal(chart.querySelector(".tr-limit-label").textContent, "REQ-SYS-001 · 125 kg");
            assert.deepEqual(byId(page, "legend").children.map(item => item.querySelector("i").className + ": " + item.textContent), [
                "best: Best options",
                "ok: Meets the constraints",
                "bad: Breaks a constraint or rule",
                "limit: REQ-SYS-001 limit"
            ]);
        });

        it("draws a lower limit on the vertical axis, named by its description", async () => {
            const constraints = [{ name: null, description: "power >= 220", attribute: "power", operator: ">=", limit: 220, limitAttribute: null, margin: 0, threshold: 220 }];

            await open(study({ constraints }));

            const chart = byId(page, "chart");
            const line = chart.querySelector(".tr-limit");
            const area = chart.querySelector(".tr-forbidden");
            assert.equal(line.getAttribute("y1"), line.getAttribute("y2"));
            assert.equal(area.getAttribute("x"), "56");
            assert.equal(area.getAttribute("width"), "488");
            assert.ok(Math.abs(Number(area.getAttribute("y")) - Number(line.getAttribute("y1"))) < 1e-9);
            assert.equal(chart.querySelector(".tr-limit-label").textContent, "power >= 220 · 220");
            assert.equal(byId(page, "legend").children.at(-1).textContent, "power limit");
        });

        it("draws an equality as its line only, since both sides of it are forbidden", async () => {
            const constraints = [{ name: "REQ-MASS", description: "mass == 120 kg", attribute: "mass", operator: "==", limit: 120, limitAttribute: null, margin: 0, threshold: 120 }];

            await open(study({ constraints }));

            const chart = byId(page, "chart");
            const line = chart.querySelector(".tr-limit");
            assert.equal(line.getAttribute("x1"), line.getAttribute("x2"));
            assert.equal(chart.querySelector(".tr-forbidden"), null);
            assert.equal(chart.querySelector(".tr-limit-label").textContent, "REQ-MASS · 120 kg");
        });

        it("draws no chart for a single attribute", async () => {
            await open(study({ attributes: [{ name: "mass", unit: "kg" }], objectives: [{ attribute: "mass", direction: "Minimize" }] }));

            assert.equal(byId(page, "chart").textContent, "One attribute only: see the list of best options.");
            assert.equal(byId(page, "legend").children.length, 0);
        });

        it("draws today alone when there is no combination to show", async () => {
            await open(study({ best: [], points: [], constraints: [] }));

            assert.equal(byId(page, "chart").querySelectorAll("g").length, 0);
            assert.equal(byId(page, "chart").querySelectorAll(".tr-today-dot").length, 1);
        });
    });

    describe("drawing the set-up", () => {
        it("lists each choice with its owner and its variants, turned on unless excluded", async () => {
            const first = study();
            first.request.excluded = ["camera.B"];

            await open(first);

            assert.deepEqual(byId(page, "choices").querySelectorAll(".tr-choice").map(heading => heading.textContent), ["camerapayloadSubsystem", "xBandTransmittercommunicationSubsystem"]);
            assert.deepEqual(byId(page, "choices").querySelectorAll("label").map(label => {
                const [box, name, values] = label.children;
                return `${box.dataset.key} ${box.checked ? "on" : "off"}: ${name.textContent} (${values.textContent})`;
            }), [
                "camera.current on: Current (38 kg · 55)",
                "camera.A on: A (30 kg · 70)",
                "camera.B off: B (34 kg · 50)",
                "xBandTransmitter.current on: Current (1.6 kg · 35)",
                "xBandTransmitter.XT-low on: XT-low (1.2 kg · 28)"
            ]);
        });

        it("shows no owner for equipment to add", async () => {
            const first = study();
            delete first.choices[1].qualifiedName;

            await open(first);

            assert.equal(byId(page, "choices").querySelectorAll(".tr-choice")[1].textContent, "xBandTransmitter");
        });

        it("lists the objectives in order, each movable when there are several", async () => {
            const first = study();
            first.request.objectives = [{ attribute: "mass", direction: "Minimize" }, { attribute: "power", direction: "Maximize" }];

            await open(first);

            assert.equal(byId(page, "objectives").querySelector(".tr-objectives").className, "tr-objectives");
            assert.deepEqual(objectiveLines(page).map(line => `${line.textContent} (${line.dataset.index}, ${line.tabIndex})`), ["1mass, lowest first (0, 0)", "2power, highest first (1, 0)"]);
        });

        it("shows a single objective without a way to move it", async () => {
            const first = study();
            first.request.objectives = [{ attribute: "mass", direction: "Minimize" }];

            await open(first);

            assert.equal(byId(page, "objectives").querySelector(".tr-objectives").className, "tr-objectives single");
            assert.equal(objectiveLines(page)[0].dataset.index, undefined);
        });

        it("lists the constraints with their switch, their limit and their margin", async () => {
            const first = study();
            first.request.constraints.push(
                { name: null, attribute: "power", operator: "<=", limit: null, unit: null, limitAttribute: "powerOutput", margin: null },
                { name: "Power", attribute: "power", operator: "<", limit: 240, unit: null, limitAttribute: null, margin: 5 });

            await open(first);

            assert.deepEqual(byId(page, "constraints").querySelectorAll(".tr-constraint").map(line => {
                const [label, limits] = line.children;
                const numbers = limits.querySelectorAll("input").map(input => `${input.ariaLabel}=${input.value}`).join(" ");
                return `${label.children[0].role} ${label.children[0].checked ? "on" : "off"} ${label.textContent} | ${limits.textContent} | ${numbers}`;
            }), [
                "switch on REQ-SYS-001 · mass ≤ | kg · margin% | limit=150 margin=20",
                "switch on Constraint 2 · power ≤ | powerOutput · margin% | margin=0",
                "switch on Power · power < |  · margin% | limit=240 margin=5"
            ]);
        });

        it("tells when there is no constraint", async () => {
            await open(study({ request: { ...study().request, objectives: [], constraints: [] }, constraints: [] }));

            assert.equal(byId(page, "constraints").textContent, "ConstraintsNo constraint.");
        });
    });

    describe("changing the settings", () => {
        it("runs the trade-off again without a variant turned off, then shows the new result", async () => {
            await open(study());

            change(field(page, "variant", "camera.A"), { checked: false });

            const call = requests(page, "tools/call").at(-1);
            assert.equal(call.params.name, "explore_trade_off");
            assert.deepEqual(call.params.arguments, { ...study().request, excluded: ["camera.A"] });
            assert.equal(byId(page, "busy").hidden, false);

            await answerStudy(page, study({ best: [study().best[2]] }));

            assert.equal(byId(page, "busy").hidden, true);
            assert.deepEqual(options(page), ["1* (tr-rank n3) | camera: B · xBandTransmitter: XT-low | 121.4 kg (−4.4) · 215 (−12) | ✓ REQ-SYS-001 · 145.68 / 150"]);
        });

        it("turns a variant on again", async () => {
            const first = study();
            first.request.excluded = ["camera.current"];
            await open(first);

            change(field(page, "variant", "camera.current"), { checked: true });

            assert.deepEqual(lastArguments(page).excluded, []);
        });

        it("leaves out a constraint switched off", async () => {
            await open(study());

            change(field(page, "enabled", "0"), { checked: false });

            assert.deepEqual(lastArguments(page).constraints, []);
        });

        it("changes the limit and the margin of a constraint, but not with an empty number", async () => {
            await open(study());

            change(field(page, "limit", "0"), { value: "155" });
            change(field(page, "margin", "0"), { value: "10" });
            change(field(page, "margin", "0"), { value: "" });

            assert.equal(requests(page, "tools/call").length, 2);
            assert.deepEqual(lastArguments(page).constraints, [{ name: "REQ-SYS-001", attribute: "mass", operator: "<=", limit: 155, unit: "kg", limitAttribute: null, margin: 10 }]);
        });

        it("ignores the changes of another field", async () => {
            await open(study());

            byId(page, "setup").dispatch("change");

            assert.equal(requests(page, "tools/call").length, 0);
        });

        it("shows the error of the solver, then hides it with the next result", async () => {
            await open(study());

            change(field(page, "enabled", "0"), { checked: false });
            await answer(page, requests(page, "tools/call").at(-1), { isError: true, content: [{ type: "text", text: "Every variant of camera is turned off." }] });

            assert.equal(byId(page, "error").textContent, "Every variant of camera is turned off.");
            assert.equal(byId(page, "error").hidden, false);
            assert.equal(byId(page, "busy").hidden, true);

            change(field(page, "enabled", "0"), { checked: true });
            await answerStudy(page, study());

            assert.equal(byId(page, "error").hidden, true);
        });

        it("tells that the solver refused the settings, or that the host refused the call", async () => {
            await open(study());

            change(field(page, "enabled", "0"), { checked: false });
            await answer(page, requests(page, "tools/call").at(-1), { isError: true });
            const refused = byId(page, "error").textContent;
            change(field(page, "enabled", "0"), { checked: true });
            await answer(page, requests(page, "tools/call").at(-1), null, { code: -32601, message: "Tool not found" });
            const notFound = byId(page, "error").textContent;
            change(field(page, "enabled", "0"), { checked: false });
            await answer(page, requests(page, "tools/call").at(-1), null, { code: -32603 });

            assert.deepEqual([refused, notFound, byId(page, "error").textContent], ["The solver refused these settings.", "Tool not found", "The host refused the call."]);
        });

        it("only shows the answer to the last call", async () => {
            await open(study());

            change(field(page, "variant", "camera.A"), { checked: false });
            change(field(page, "variant", "camera.B"), { checked: false });
            change(field(page, "enabled", "0"), { checked: false });
            const [first, second, third] = requests(page, "tools/call");
            await answer(page, first, { structuredContent: study({ best: [] }) });
            await answer(page, second, null, { code: -32603, message: "late" });

            assert.equal(options(page).length, 3);
            assert.equal(byId(page, "error").hidden, true);
            assert.equal(byId(page, "busy").hidden, false);

            await answer(page, third, { structuredContent: study({ best: [study().best[1]] }) });

            assert.equal(options(page).length, 1);
            assert.equal(byId(page, "busy").hidden, true);
        });
    });

    describe("ordering the objectives", () => {
        beforeEach(async () => {
            await open(study());
            placeObjectives(page);
        });

        it("moves an objective down with the arrow key, solves again and keeps the focus on it", () => {
            const event = objectiveLines(page)[0].dispatch("keydown", { key: "ArrowDown" });

            assert.equal(event.defaultPrevented, true);
            assert.deepEqual(lastArguments(page).objectives.map(objective => objective.attribute), ["power", "mass"]);
            assert.deepEqual(objectiveLines(page).map(line => line.textContent), ["1power, lowest first", "2mass, lowest first"]);
            assert.equal(page.document.activeElement, objectiveLines(page)[1]);
        });

        it("ignores the arrow keys beyond the ends, the other keys, and the keys outside an objective", () => {
            const up = objectiveLines(page)[0].dispatch("keydown", { key: "ArrowUp" });
            objectiveLines(page)[1].dispatch("keydown", { key: "ArrowDown" });
            objectiveLines(page)[0].dispatch("keydown", { key: "Enter" });
            byId(page, "objectives").dispatch("keydown", { key: "ArrowDown" });

            assert.equal(up.defaultPrevented, false);
            assert.equal(requests(page, "tools/call").length, 0);
        });

        it("drags an objective below the next one, renumbers them, then solves again when dropped", () => {
            const [mass, power] = objectiveLines(page);

            mass.children[1].dispatch("pointerdown", { button: 0, pointerId: 7, clientY: 110 });
            assert.equal(mass.pointerCapture, 7);
            assert.equal(mass.classList.contains("dragging"), true);

            byId(page, "objectives").dispatch("pointermove", { clientY: 150 });
            assert.deepEqual(objectiveLines(page), [power, mass]);
            assert.deepEqual(objectiveLines(page).map(line => line.querySelector(".tr-rank").textContent), ["1", "2"]);
            assert.equal(mass.style.transform, "translateY(40px)");

            byId(page, "objectives").dispatch("pointerup", {});
            assert.equal(mass.classList.contains("dragging"), false);
            assert.equal(mass.style.transform, "");
            assert.deepEqual(lastArguments(page).objectives.map(objective => objective.attribute), ["power", "mass"]);
        });

        it("drags an objective above the previous one", () => {
            const [mass, power] = objectiveLines(page);

            power.dispatch("pointerdown", { button: 0, pointerId: 1, clientY: 140 });
            byId(page, "objectives").dispatch("pointermove", { clientY: 105 });

            assert.deepEqual(objectiveLines(page), [power, mass]);
        });

        it("puts the objective back when the drag is cancelled", () => {
            objectiveLines(page)[0].dispatch("pointerdown", { button: 0, pointerId: 7, clientY: 110 });
            byId(page, "objectives").dispatch("pointermove", { clientY: 150 });

            byId(page, "objectives").dispatch("pointercancel", {});

            assert.deepEqual(objectiveLines(page).map(line => line.textContent), ["1mass, lowest first", "2power, lowest first"]);
            assert.equal(requests(page, "tools/call").length, 0);
        });

        it("does nothing when the objective is dropped at its place, or grabbed with another button", () => {
            const [mass] = objectiveLines(page);
            mass.dispatch("pointerdown", { button: 0, pointerId: 7, clientY: 110 });
            byId(page, "objectives").dispatch("pointerup", {});

            mass.dispatch("pointerdown", { button: 2, pointerId: 7, clientY: 110 });
            byId(page, "objectives").dispatch("pointermove", { clientY: 150 });
            byId(page, "objectives").dispatch("pointerup", {});
            byId(page, "objectives").dispatch("pointerdown", { button: 0, pointerId: 7, clientY: 110 });

            assert.equal(mass.classList.contains("dragging"), false);
            assert.deepEqual(objectiveLines(page).map(line => line.textContent), ["1mass, lowest first", "2power, lowest first"]);
            assert.equal(requests(page, "tools/call").length, 0);
        });
    });

    describe("choosing an option", () => {
        it("selects an option clicked in the list or on the chart", async () => {
            await open(study());

            byId(page, "options").querySelectorAll(".tr-option")[1].querySelector(".tr-name").dispatch("click");
            const fromList = [options(page)[1], byId(page, "pick").children[0].textContent, byId(page, "pick").querySelector("button").textContent];
            byId(page, "chart").querySelectorAll("g").find(group => group.dataset.option === 3).querySelector("text").dispatch("click");

            assert.deepEqual(fromList, [
                "2* (tr-rank n2) | camera: A · xBandTransmitter: current | 117.8 kg (−8) · 242 (+15) | ✓ REQ-SYS-001 · 141.36 / 150",
                "Option 2, chosen by you: camera: A · xBandTransmitter: current",
                "Use option 2"
            ]);
            assert.equal(options(page)[2].startsWith("3*"), true);
            assert.equal(points(page).at(-1).startsWith("pt pt-best pt-rank3 | 3"), true);
        });

        it("ignores a click outside the options", async () => {
            await open(study());

            byId(page, "options").dispatch("click");
            byId(page, "chart").dispatch("click");

            assert.equal(byId(page, "pick").children[0].textContent, "Best option for these settings: camera: A · xBandTransmitter: XT-low");
        });

        it("puts the message of the option in the chat box", async () => {
            await open(study());
            const button = byId(page, "pick").querySelector("button");

            button.click();

            const message = requests(page, "ui/message").at(-1);
            assert.equal(button.disabled, true);
            assert.deepEqual(message.params, {
                role: "user",
                content: [{
                    type: "text",
                    text: [
                        "Use option 1 of the trade-off \"Camera and X-band transmitter\":",
                        "- camera (part 558a0ae4-8585-66a3-9bc6-52322650941c): variant A (mass 30 kg, power 70)",
                        "- xBandTransmitter (part 324f00ec-273e-5fe1-c724-a63af46fee09): variant XT-low (mass 1.2 kg, power 28)",
                        "Totals: mass 117.4 kg (−8.4), power 235 (+8).",
                        "Prepare the batch with apply_changes."
                    ].join("\n")
                }]
            });

            await answer(page, message, {});

            assert.equal(byId(page, "pick").querySelector(".tr-note").textContent, "The message is in the chat box: check it and press Enter.");
            assert.equal(button.textContent, "Sent");
        });

        it("gives the message to the assistant when the host does not put it in the chat box", async () => {
            await open(study());
            const button = byId(page, "pick").querySelector("button");

            button.click();
            await answer(page, requests(page, "ui/message").at(-1), { isError: true });
            const context = requests(page, "ui/update-model-context").at(-1);
            await answer(page, context, {});

            assert.equal(context.params.content[0].text, requests(page, "ui/message").at(-1).params.content[0].text);
            assert.equal(byId(page, "pick").querySelector(".tr-note").textContent, "Sent to the assistant: type \"go\" in the chat.");
            assert.equal(button.textContent, "Sent");
        });

        it("tells that the host refused the message, and lets the user try again", async () => {
            await open(study());
            const button = byId(page, "pick").querySelector("button");

            button.click();
            await answer(page, requests(page, "ui/message").at(-1), null, { code: -32601, message: "Method not found" });
            await answer(page, requests(page, "ui/update-model-context").at(-1), null, { code: -32601, message: "Method not found" });

            assert.equal(byId(page, "pick").querySelector(".tr-note").textContent, "The host refused the message.");
            assert.equal(button.disabled, false);
            assert.equal(button.textContent, "Use option 1");
        });

        it("tells the assistant the settings changed in the view", async () => {
            const first = study();
            first.request.constraints.push({ name: null, attribute: "power", operator: "<=", limit: 240, unit: null, limitAttribute: null, margin: null });
            await open(first);
            placeObjectives(page);

            change(field(page, "variant", "camera.B"), { checked: false });
            objectiveLines(page)[0].dispatch("keydown", { key: "ArrowDown" });
            change(field(page, "limit", "0"), { value: "155" });
            change(field(page, "margin", "0"), { value: "10" });
            change(field(page, "limit", "1"), { value: "250" });
            await answerStudy(page, study({ best: [study().best[1]] }));
            byId(page, "pick").querySelector("button").click();

            const text = requests(page, "ui/message").at(-1).params.content[0].text.split("\n");
            assert.deepEqual(text, [
                "Use option 1 of the trade-off \"Camera and X-band transmitter\":",
                "- camera (part 558a0ae4-8585-66a3-9bc6-52322650941c): variant A (mass 30 kg, power 70)",
                "Totals: mass 117.8 kg (−8), power 242 (+15).",
                "Settings changed in the view: camera.B turned off; objectives in order: power, mass; REQ-SYS-001 limit 155 kg; REQ-SYS-001 margin 10 %; power <= limit 250.",
                "Prepare the batch with apply_changes."
            ]);
        });

        it("tells the assistant that a constraint is turned off", async () => {
            await open(study());

            change(field(page, "enabled", "0"), { checked: false });
            await answerStudy(page, study());
            byId(page, "pick").querySelector("button").click();

            assert.match(requests(page, "ui/message").at(-1).params.content[0].text, /Settings changed in the view: REQ-SYS-001 turned off\./);
        });
    });
});
