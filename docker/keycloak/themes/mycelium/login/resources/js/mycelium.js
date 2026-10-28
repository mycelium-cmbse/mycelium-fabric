// Mycelium login theme: remembers the identity provider a user last chose on this browser and marks
// its row "Last used" (Figma "Proto / Sign In"). Nothing leaves the browser; the alias is all that is stored.
(function () {
  "use strict";
  var KEY = "mycelium.lastIdentityProvider";

  function read() {
    try { return window.localStorage.getItem(KEY); } catch (e) { return null; }
  }

  function write(alias) {
    try { window.localStorage.setItem(KEY, alias); } catch (e) { /* private mode: no memory, no harm */ }
  }

  document.addEventListener("DOMContentLoaded", function () {
    var last = read();
    var rows = document.querySelectorAll(".mycelium-provider[data-alias]");
    for (var i = 0; i < rows.length; i++) {
      var row = rows[i];
      if (row.getAttribute("data-alias") === last) {
        var badge = row.querySelector(".mycelium-provider__last");
        if (badge) badge.hidden = false;
      }
      row.addEventListener("click", function (event) {
        write(event.currentTarget.getAttribute("data-alias"));
      });
    }
  });
})();
