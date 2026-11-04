// ------------------------------------------------------------------------------------------------
//  <copyright file="script.test.js" company="Starion Group S.A.">
//
//    Copyright 2026 Starion Group S.A.
//    SPDX-License-Identifier: Apache-2.0
//
//  </copyright>
//  ------------------------------------------------------------------------------------------------

// Tests of the view "Batch review" (McpApp/script.js), run by Node.js: node --test Mycelium.Fabric.Mcp.Tests/McpApp/script.test.js

const { describe, it, beforeEach } = require("node:test");
const assert = require("node:assert/strict");
const path = require("node:path");

const { loadPage } = require("./fake-page.js");

const APP_FOLDER = path.join(__dirname, "..", "..", "Mycelium.Fabric.Mcp", "McpApp");
const HOST_ORIGIN = "https://host.example";

/**
 * Builds the preview of a batch, empty unless told otherwise.
 * @param {object} [values] The values that replace the empty ones.
 * @returns {object} The preview, as BatchPreview gives it.
 */
function preview(values) {
    return {
        batchId: "4f0c8a52-6c1e-4a8e-9a43-0b6e8f1f2a10",
        token: "token-1",
        createdElements: [],
        modifiedElements: [],
        deletedElements: [],
        budgetImpacts: [],
        requirementChanges: [],
        newIssues: [],
        newIssueCount: 0,
        resolvedIssueCount: 0,
        ...values
    };
}

/**
 * Builds the notification of the host that gives the result of apply_changes.
 * @param {object} structuredContent The review of the batch, or nothing.
 * @param {string} [text] The text of the result.
 * @returns {object} The notification.
 */
function toolResult(structuredContent, text = "") {
    return { jsonrpc: "2.0", method: "ui/notifications/tool-result", params: { content: [{ type: "text", text }], structuredContent } };
}

/**
 * Reads the rows of the list of changes.
 * @param {object} page The page.
 * @returns {object[]} For each row: its kind, depth, name, value and Ownership.
 */
function rows(page) {
    return page.document.getElementById("changes").children.filter(row => row.className.startsWith("cr ")).map(row => {
        const [operation, , name, ...right] = row.children;
        const value = right.find(element => element.className === "v");
        const ownership = right.find(element => element.className === "own");

        return {
            kind: row.className.slice("cr ".length),
            depth: Number.parseInt(operation.style.marginLeft, 10) / 20,
            name: name.textContent,
            value: value?.textContent ?? null,
            ownership: ownership?.textContent ?? null
        };
    });
}

/**
 * Reads the checks of the tab "Checks".
 * @param {object} page The page.
 * @returns {string[]} For each check: its kind, title and detail.
 */
function checks(page) {
    return page.document.getElementById("checks").children.map(check => `${check.className.slice("chk ".length)}: ${check.children[1].textContent}`);
}

/**
 * Reads the texts of the elements that match selectors.
 * @param {object} page The page.
 * @param {string} id The id of the element that holds them.
 * @param {string} selectors The selectors.
 * @returns {string[]} The texts.
 */
function texts(page, id, selectors) {
    return page.document.getElementById(id).querySelectorAll(selectors).map(element => element.textContent);
}

/**
 * Gets the messages that the view has sent to the host.
 * @param {object} page The page.
 * @returns {object[]} The messages.
 */
function messages(page) {
    return page.sent.map(entry => entry.message);
}

describe("review-batch view", () => {
    let page;

    beforeEach(() => {
        page = loadPage(path.join(APP_FOLDER, "review-batch.html"), path.join(APP_FOLDER, "script.js"));
    });

    describe("talking with the host", () => {
        it("initializes with the parent frame, whatever its origin", () => {
            assert.deepEqual(page.sent, [{
                message: {
                    jsonrpc: "2.0",
                    id: 1,
                    method: "ui/initialize",
                    params: { protocolVersion: "2026-01-26", appInfo: { name: "review-batch", version: "0.1.0" }, appCapabilities: {} }
                },
                targetOrigin: "*"
            }]);
        });

        it("applies the theme of the host, then sends the next messages to its origin only", () => {
            page.receive({ jsonrpc: "2.0", id: 1, result: { hostContext: { theme: "dark", styles: { variables: { "--color-background-primary": "#262624", "--font-sans": "" } } } } });

            const style = page.document.documentElement.style;
            assert.equal(style["--color-background-primary"], "#262624");
            assert.equal(style["--font-sans"], undefined);
            assert.equal(style.colorScheme, "dark");
            assert.deepEqual(page.sent.at(-1), { message: { jsonrpc: "2.0", method: "ui/notifications/initialized", params: {} }, targetOrigin: HOST_ORIGIN });
        });

        it("ignores the messages of another frame, of another origin, or that are not JSON-RPC", () => {
            page.receive({ jsonrpc: "2.0", id: 1, result: {} });
            const count = page.sent.length;

            page.receive({ jsonrpc: "2.0", id: 7, method: "ping" }, HOST_ORIGIN, {});
            page.receive({ jsonrpc: "2.0", id: 7, method: "ping" }, "https://other.example");
            page.receive({ id: 7, method: "ping" });
            page.receive(null);

            assert.equal(page.sent.length, count);
        });

        it("keeps any origin for a host whose frame has none", () => {
            page.receive({ jsonrpc: "2.0", id: 1, result: {} }, "null");

            assert.equal(page.sent.at(-1).targetOrigin, "*");
        });

        it("answers the requests of the host with an empty result", () => {
            page.receive({ jsonrpc: "2.0", id: 9, method: "ui/resource-teardown", params: {} });

            assert.deepEqual(messages(page).at(-1), { jsonrpc: "2.0", id: 9, result: {} });
        });

        it("follows the changes of theme of the host, and ignores the other notifications", () => {
            page.receive({ jsonrpc: "2.0", method: "ui/notifications/host-context-changed", params: { theme: "light" } });
            page.receive({ jsonrpc: "2.0", method: "ui/notifications/host-context-changed" });
            page.receive({ jsonrpc: "2.0", method: "ui/notifications/tool-input", params: {} });

            assert.equal(page.document.documentElement.style.colorScheme, "light");
            assert.equal(page.sent.length, 1);
        });

        it("tells the host the size of the view", () => {
            page.resize();

            assert.deepEqual(messages(page).at(-1), { jsonrpc: "2.0", method: "ui/notifications/size-changed", params: { width: 600, height: 413 } });
            assert.equal(page.document.documentElement.style.height, undefined);
        });
    });

    describe("showing a batch that waits for the decision of the user", () => {
        it("shows the created elements as a tree, with their definition, multiplicity, value and Ownership", () => {
            page.receive(toolResult({
                status: "Pending",
                preview: preview({
                    createdElements: [
                        { qualifiedName: "EOSat1::eosat1::thermal", type: "PartUsage", value: "3.1 kg · 12 W", ownership: "THE" },
                        { qualifiedName: "EOSat1::eosat1::thermal::mass", type: "AttributeUsage", value: "3.1 kg", ownership: "THE" },
                        { qualifiedName: "EOSat1::eosat1::thermal::heater", type: "PartUsage", definition: "Heater", multiplicity: "4", value: "12 W", ownership: "THE" },
                        { qualifiedName: "EOSat1::eosat1::thermal::heater::power", type: "AttributeUsage", value: "3 W", ownership: "THE" },
                        { qualifiedName: "EOSat1::eosat1::thermal::loop", type: "PartUsage", ownership: "XYZ" },
                        { qualifiedName: "EOSat1::eosat1::emissivity", type: "AttributeUsage", value: "0.8" },
                        { qualifiedName: null, type: "SatisfyRequirementUsage" }
                    ]
                })
            }));

            assert.deepEqual(rows(page), [
                { kind: "add", depth: 0, name: "thermal in eosat1", value: "3.1 kg · 12 W", ownership: "THE" },
                { kind: "add", depth: 1, name: "heater : Heater [4]", value: "12 W", ownership: null },
                { kind: "add", depth: 1, name: "loop", value: null, ownership: "XYZ" },
                { kind: "add", depth: 0, name: "emissivity in eosat1", value: "0.8", ownership: null },
                { kind: "add", depth: 0, name: "SatisfyRequirementUsage", value: null, ownership: null }
            ]);
            assert.deepEqual(texts(page, "pills", ".pill"), ["+7"]);
            assert.equal(page.document.getElementById("target_state").textContent, "Waiting for your decision");
        });

        it("colors a known Ownership as Bloom does, and any other one with a fixed color", () => {
            page.receive(toolResult({
                status: "Pending",
                preview: preview({
                    createdElements: [
                        { qualifiedName: "M::a", type: "PartUsage", ownership: "THE" },
                        { qualifiedName: "M::b", type: "PartUsage", ownership: "XYZ" },
                        { qualifiedName: "M::c", type: "PartUsage", ownership: "XYZ" }
                    ]
                })
            }));

            const colors = page.document.getElementById("changes").querySelectorAll(".own").map(ownership => ownership.style["--oc"]);
            assert.equal(colors[0], "#EA580C");
            assert.match(colors[1], /^#[0-9A-F]{6}$/);
            assert.equal(colors[2], colors[1]);
        });

        it("gives each family of metaclass its own icon", () => {
            const types = ["SatisfyRequirementUsage", "RequirementUsage", "PartDefinition", "AttributeDefinition", "PortUsage", "AttributeUsage", "Package", "PartUsage"];
            page.receive(toolResult({ status: "Pending", preview: preview({ modifiedElements: types.map((type, index) => ({ qualifiedName: `M::e${index}`, type })) }) }));

            const icons = page.document.getElementById("changes").children.map(row => row.children[1].children[0].innerHTML);
            assert.equal(new Set(icons).size, types.length);
        });

        it("shows the created definitions on one row, under the usages that they type", () => {
            page.receive(toolResult({
                status: "Pending",
                preview: preview({
                    createdElements: [
                        { qualifiedName: "EOSat1::Parts::Heater", type: "PartDefinition" },
                        { qualifiedName: "EOSat1::Parts::Heater::power", type: "AttributeUsage", value: "3 W" },
                        { qualifiedName: "EOSat1::Parts::Radiator", type: "PartDefinition" },
                        { qualifiedName: "EOSat1::eosat1::thermal::heater", type: "PartUsage", definition: "Heater", ownership: "THE" },
                        { qualifiedName: "EOSat1::eosat1::thermal::radiator", type: "PartUsage", definition: "Radiator", ownership: "THE" }
                    ]
                })
            }));

            assert.deepEqual(rows(page), [
                { kind: "add", depth: 0, name: "Heater, Radiator in Parts", value: "2 defs", ownership: "THE" },
                { kind: "add", depth: 0, name: "heater : Heater in thermal", value: null, ownership: "THE" },
                { kind: "add", depth: 1, name: "power", value: "3 W", ownership: null },
                { kind: "add", depth: 0, name: "radiator : Radiator in thermal", value: null, ownership: "THE" }
            ]);
        });

        it("shows a created definition with its own row when no created usage uses it and it has features", () => {
            page.receive(toolResult({
                status: "Pending",
                preview: preview({
                    createdElements: [
                        { qualifiedName: "Lib::Port", type: "PortDefinition" },
                        { qualifiedName: "Lib::Port::flow", type: "AttributeUsage" },
                        { qualifiedName: "Lib::Unused", type: "AttributeDefinition" }
                    ]
                })
            }));

            assert.deepEqual(rows(page).map(row => [row.depth, row.name, row.value]), [[0, "Port in Lib", null], [1, "flow", null], [0, "Unused in Lib", "1 def"]]);
        });

        it("shows the modified and deleted elements, and the totals that the batch changes", () => {
            page.receive(toolResult({
                status: "Pending",
                preview: preview({
                    modifiedElements: [{ qualifiedName: "EOSat1::eosat1::camera::mass", type: "AttributeUsage", value: "38 kg → 41 kg", ownership: "PAY" }],
                    deletedElements: [{ qualifiedName: "EOSat1::eosat1::camera::lens", type: "PartUsage", ownership: "PAY" }],
                    budgetImpacts: [
                        { part: "EOSat1::eosat1", attribute: "mass", before: 125.8, after: 128.8, unit: "kg" },
                        { part: "EOSat1::eosat1", attribute: "power", before: 227, after: 225 },
                        { part: "EOSat1::bench", attribute: "mass", before: 3, after: 3.5, unit: "kg" }
                    ]
                })
            }));

            assert.deepEqual(rows(page), [
                { kind: "mod", depth: 0, name: "mass in camera", value: "38 kg → 41 kg", ownership: "PAY" },
                { kind: "del", depth: 0, name: "lens in camera", value: null, ownership: "PAY" },
                { kind: "mod", depth: 0, name: "eosat1::mass", value: "125.8 → 128.8 kg", ownership: null },
                { kind: "mod", depth: 0, name: "eosat1::power", value: "227 → 225", ownership: null },
                { kind: "mod", depth: 0, name: "bench::mass", value: "3 → 3.5 kg", ownership: null }
            ]);
            assert.deepEqual(texts(page, "pills", ".pill"), ["~1", "−1"]);
            assert.deepEqual(texts(page, "impacts", ".tagv"), ["eosat1 Mass +3 kg", "Power −2", "bench Mass +0.5 kg"]);
            assert.equal(page.document.getElementById("impacts").hidden, false);
        });

        it("shows an empty batch", () => {
            page.receive(toolResult({ status: "Pending", preview: preview() }));

            assert.equal(page.document.getElementById("changes").textContent, "No change");
            assert.equal(page.document.getElementById("impacts").hidden, true);
            assert.deepEqual(checks(page), ["ok: Validation: no new issuevalidate_model on the model with the batch applied"]);
            assert.equal(page.document.getElementById("check_count").hidden, true);
        });

        it("shows the requirements that the batch changes and the new issues", () => {
            page.receive(toolResult({
                status: "Pending",
                preview: preview({
                    requirementChanges: [
                        { reqId: "REQ-SYS-001", name: "dryMass", satisfiedBy: "EOSat1::eosat1", statusBefore: "Satisfied", statusAfter: "NotSatisfied", value: "128.8 kg" },
                        { reqId: "REQ-SYS-001", name: "dryMass", statusBefore: "Satisfied", statusAfter: "NotSatisfied" },
                        { reqId: "REQ-SYS-005", statusBefore: "NotSatisfied", statusAfter: "NotSatisfied", gapChange: -2 },
                        { name: "power", statusBefore: "NotSatisfied", statusAfter: "Satisfied" },
                        { reqId: "REQ-THE-001", statusBefore: "NotSatisfied", statusAfter: "NotSatisfied", gapChange: 1 },
                        { reqId: "REQ-THE-002", statusBefore: null, statusAfter: "NotVerifiable" },
                        { reqId: "REQ-THE-003", statusBefore: "Satisfied", statusAfter: null }
                    ],
                    newIssues: [
                        { severity: "Error", message: "The part has no definition", element: "EOSat1::eosat1::loop", rule: "PartWithoutDefinition" },
                        { severity: "Warning", message: "The attribute has no value" },
                        { severity: "Info", message: "Unused definition", rule: "UnusedDefinition" }
                    ],
                    newIssueCount: 5,
                    resolvedIssueCount: 1
                })
            }));

            assert.deepEqual(texts(page, "impacts", ".tagv"), ["REQ-SYS-001 fails", "REQ-SYS-005 worse", "power met", "5 new issues"]);
            assert.deepEqual(checks(page), [
                "bad: REQ-SYS-001 · dryMasseosat1 · 128.8 kg · satisfied → not satisfied",
                "bad: REQ-SYS-001 · dryMasssatisfied → not satisfied",
                "bad: REQ-SYS-005not satisfied",
                "ok: powernot satisfied → satisfied",
                "bad: REQ-THE-001not satisfied",
                "info: REQ-THE-002new → not verifiable",
                "info: REQ-THE-003satisfied → deleted",
                "bad: The part has no definitionEOSat1::eosat1::loop · PartWithoutDefinition",
                "warn: The attribute has no value",
                "info: Unused definitionUnusedDefinition",
                "info: … and 2 more new issues",
                "ok: 1 existing issue resolved"
            ]);
            assert.equal(page.document.getElementById("check_count").textContent, "6");
            assert.equal(page.document.getElementById("check_count").hidden, false);
        });

        it("tells the issues that the batch resolves when it adds none", () => {
            page.receive(toolResult({ status: "Pending", preview: preview({ resolvedIssueCount: 2 }) }));

            assert.deepEqual(checks(page), ["ok: Validation: no new issue2 existing issues resolved"]);
        });

        it("tells that the buttons cannot decide a batch without token", () => {
            page.receive(toolResult({ status: "Pending", preview: preview({ token: undefined }) }));

            assert.equal(page.document.getElementById("target_state").textContent, "The batch has no token: the buttons cannot decide it.");
        });

        it("tells that it cannot show a batch whose preview is incomplete", () => {
            page.receive(toolResult({ status: "Pending", preview: { batchId: "b", token: "t" } }));

            assert.match(page.document.getElementById("target_state").textContent, /^The view could not show the batch: /);
        });

        it("switches between the tabs", () => {
            const [changesTab, checksTab] = page.document.querySelectorAll(".seg button");
            checksTab.click();

            assert.deepEqual(page.document.querySelectorAll(".seg button, .segp").filter(element => element.classList.contains("on")), [checksTab, page.document.querySelectorAll(".segp")[1]]);

            changesTab.click();

            assert.equal(changesTab.classList.contains("on"), true);
            assert.equal(checksTab.classList.contains("on"), false);
        });
    });

    describe("showing a batch that cannot be decided", () => {
        it("shows the problems of a batch refused by Fabric", () => {
            page.receive(toolResult({ status: "Refused", result: { problems: ["Change 2: the owner 'thermal' does not exist."] } }));

            assert.equal(page.document.getElementById("target_state").textContent, "Refused by Fabric: the assistant will correct it");
            assert.equal(page.document.getElementById("changes").textContent, "Nothing was applied");
            assert.deepEqual(checks(page), ["bad: Change 2: the owner 'thermal' does not exist."]);
            assert.equal(page.document.getElementById("impacts").hidden, true);
            assert.equal(page.document.querySelectorAll(".segp")[1].classList.contains("on"), true);
            assert.equal(page.document.getElementById("note").textContent, "Nothing was applied: the assistant has the problems and corrects the batch.");
            assert.equal(page.document.getElementById("button_zone").hidden, true);
            assert.equal(page.document.getElementById("comment_zone").hidden, true);
        });

        it("hides the buttons for a batch already decided", () => {
            page.receive(toolResult({ status: "Applied" }));

            assert.equal(page.document.getElementById("target_state").textContent, "Applied to the model");
            assert.equal(page.document.getElementById("button_zone").hidden, true);
            assert.equal(page.document.getElementById("note").hidden, true);
        });

        it("shows the text of a result that is not a review", () => {
            page.receive(toolResult(undefined, "An error occurred invoking 'apply_changes'."));

            assert.equal(page.document.getElementById("target_state").textContent, "The batch could not be read");
            assert.equal(page.document.getElementById("note").className, "note err");
            assert.equal(page.document.getElementById("note").textContent, "An error occurred invoking 'apply_changes'.");
            assert.equal(page.document.getElementById("button_zone").hidden, true);
        });
    });

    describe("deciding the batch", () => {
        beforeEach(() => {
            page.receive(toolResult({ status: "Pending", preview: preview() }));
            page.document.getElementById("comment1").value = "  Keep the old heater.  ";
        });

        it("applies the batch with the comment of the user", () => {
            page.document.getElementById("btn_apply").click();

            assert.deepEqual(messages(page).at(-1), {
                jsonrpc: "2.0",
                id: 2,
                method: "tools/call",
                params: { name: "decide_batch", arguments: { batchId: "4f0c8a52-6c1e-4a8e-9a43-0b6e8f1f2a10", token: "token-1", apply: true, comment: "Keep the old heater." } }
            });
            assert.equal(page.document.getElementById("btn_apply").disabled, true);
            assert.equal(page.document.getElementById("btn_cancel").disabled, true);
        });

        it("rejects the batch with the comment of the user", () => {
            page.document.getElementById("btn_cancel").click();

            assert.equal(messages(page).at(-1).id, 3);
            assert.deepEqual(messages(page).at(-1).params.arguments, { batchId: "4f0c8a52-6c1e-4a8e-9a43-0b6e8f1f2a10", token: "token-1", apply: false, comment: "Keep the old heater." });
            assert.equal(page.document.getElementById("btn_apply").disabled, true);
        });

        it("shows the applied batch and gives its message to the assistant", () => {
            page.receive({ jsonrpc: "2.0", id: 2, result: { structuredContent: { status: "Applied", message: "The batch has been applied." } } });

            assert.equal(page.document.getElementById("target_state").textContent, "Applied to the model");
            assert.equal(page.document.getElementById("note").className, "done");
            assert.equal(page.document.getElementById("note").textContent, "Applied.");
            assert.equal(page.document.getElementById("button_zone").hidden, true);
            assert.deepEqual(messages(page).at(-1), {
                jsonrpc: "2.0",
                id: 4,
                method: "ui/update-model-context",
                params: { content: [{ type: "text", text: "The batch has been applied." }] }
            });
        });

        it("shows the rejected batch", () => {
            page.receive({ jsonrpc: "2.0", id: 3, result: { structuredContent: { status: "Rejected", message: "The user has rejected the batch." } } });

            assert.equal(page.document.getElementById("target_state").textContent, "Rejected");
            assert.equal(page.document.getElementById("note").className, "done rej");
            assert.equal(messages(page).at(-1).params.content[0].text, "The user has rejected the batch.");
        });

        it("shows the problems of a batch that Fabric refuses when the user applies it", () => {
            page.receive({ jsonrpc: "2.0", id: 2, result: { structuredContent: { status: "Refused", message: "Nothing was applied.", result: { problems: ["The model has changed."] } } } });

            assert.deepEqual(checks(page), ["bad: The model has changed."]);
            assert.equal(page.document.getElementById("note").className, "done rej");
            assert.equal(page.document.getElementById("note").textContent, "Nothing was applied.");
            assert.equal(page.document.getElementById("check_count").textContent, "1");
        });

        it("tells that the decision has failed", () => {
            page.receive({ jsonrpc: "2.0", id: 2, result: { isError: true, content: [{ type: "text", text: "The token is not valid." }] } });

            assert.equal(page.document.getElementById("target_state").textContent, "The decision has failed");
            assert.equal(page.document.getElementById("note").textContent, "The token is not valid.");

            page.receive({ jsonrpc: "2.0", id: 3, error: { code: -32603, message: "Internal error" } });

            assert.equal(page.document.getElementById("note").textContent, "");
            assert.equal(page.document.getElementById("note").className, "note err");
        });

        it("ignores the responses to the other requests", () => {
            const count = page.sent.length;
            page.receive({ jsonrpc: "2.0", id: 4, result: {} });

            assert.equal(page.sent.length, count);
            assert.equal(page.document.getElementById("target_state").textContent, "Waiting for your decision");
        });
    });
});
