// ===== Section Toggle =====
function toggleSection(header) {
  header.closest('.section-card').classList.toggle('open');
}

function toggleAll() {
  // Toggle section-card elements
  var cards = document.querySelectorAll('.section-card');
  var allOpen = Array.from(cards).every(function(c) { return c.classList.contains('open'); });
  cards.forEach(function(c) {
    if (allOpen) { c.classList.remove('open'); } else { c.classList.add('open'); }
  });
  // Toggle legacy collapsible/content elements
  var contents = document.querySelectorAll('.collapsible + .content');
  var allVisible = Array.from(contents).every(function(c) { return c.style.display !== 'none' && c.style.display !== ''; });
  contents.forEach(function(c) {
    c.style.display = (allOpen || allVisible) ? 'none' : 'block';
  });
}

// ===== Row-Level Detail Toggle (Orphaned & Superseded Backups) =====
// Each job row is immediately followed by a sibling <tr class="detail-row">
// holding the per-object breakdown, hidden by default. Toggle it directly
// via inline style rather than a class, matching the existing Legacy
// Collapsible Toggle's approach for <table>-shaped content below a button.
function toggleDetailRow(rowElement) {
  var detailRow = rowElement.nextElementSibling;
  if (detailRow && detailRow.classList.contains('detail-row')) {
    detailRow.style.display = (detailRow.style.display === 'table-row') ? 'none' : 'table-row';
  }
}

// ===== Legacy Collapsible Toggle (for sections using SectionStartWithButton) =====
// The element a collapsible button reveals is simply whatever follows it. Historically
// that was a <div class="content">, but the VB365 stat sections (Job Statistics,
// Processing Stats, Job Sessions) place a bare <table> there. The old guard only
// toggled siblings with class="content", so those tables stayed permanently collapsed
// (issue #49). Toggle the next sibling regardless of class. Reveal by clearing the
// inline display so each element returns to its natural value (table -> table,
// div -> block) instead of forcing 'block', which would break <table> column layout.
document.addEventListener('DOMContentLoaded', function() {
  document.querySelectorAll('.collapsible').forEach(function(btn) {
    btn.addEventListener('click', function() {
      var content = this.nextElementSibling;
      if (content) {
        content.style.display = (content.style.display === 'none') ? '' : 'none';
      }
    });
  });
});

// ===== Table Sort =====
function sortTableByColumn(table, column, asc) {
  if (asc === undefined) asc = true;
  var dirModifier = asc ? 1 : -1;
  var tBody = table.tBodies[0];
  if (!tBody) return;
  // Direct children only, not tBody.querySelectorAll("tr"): the Orphaned &
  // Superseded Backups table nests a full <table> (the per-object
  // breakdown) inside a <td colspan> inside a sibling <tr class="detail-row">.
  // A descendant query would also match that inner table's own <tr>s, and
  // appendChild()-ing an already-attached node MOVES it - flattening the
  // nested table into this one and destroying the toggle/detail-row pairing.
  // Each "detail-row" is paired with the primary row immediately before it
  // and must move together with it, not be sorted independently.
  var directRows = Array.prototype.filter.call(tBody.children, function(el) {
    return el.tagName === "TR";
  });
  var groups = [];
  // claimedAsDetail tracks which detail-row indices were already consumed
  // as some earlier primary row's paired detail, so the check below can
  // tell "already handled, safe to skip" apart from "never claimed by
  // anyone" (e.g. two consecutive detail-rows, or a leading detail-row with
  // nothing before it) - the latter must still become its own group, not
  // silently vanish when tBody is emptied and rebuilt from `groups` below.
  var claimedAsDetail = {};
  for (var g = 0; g < directRows.length; g++) {
    if (directRows[g].classList.contains("detail-row")) {
      if (claimedAsDetail[g]) { continue; }
      groups.push({ primary: directRows[g], detail: null });
      continue;
    }
    var next = directRows[g + 1];
    var detail = (next && next.classList.contains("detail-row")) ? next : null;
    if (detail) { claimedAsDetail[g + 1] = true; }
    groups.push({ primary: directRows[g], detail: detail });
  }
  var sortedGroups = groups.sort(function(a, b) {
    var aCell = a.primary.querySelector("td:nth-child(" + (column + 1) + ")");
    var bCell = b.primary.querySelector("td:nth-child(" + (column + 1) + ")");
    if (!aCell || !bCell) return 0;
    var aColText = aCell.textContent.trim();
    var bColText = bCell.textContent.trim();
    var aNum = /^\d/.test(aColText);
    var bNum = /^\d/.test(bColText);
    if (aNum && bNum) {
      return (parseFloat(aColText) - parseFloat(bColText)) * dirModifier;
    } else {
      return aColText > bColText ? (1 * dirModifier) : (-1 * dirModifier);
    }
  });
  while (tBody.firstChild) { tBody.removeChild(tBody.firstChild); }
  for (var i = 0; i < sortedGroups.length; i++) {
    tBody.appendChild(sortedGroups[i].primary);
    if (sortedGroups[i].detail) { tBody.appendChild(sortedGroups[i].detail); }
  }
  table.querySelectorAll("th").forEach(function(th) {
    th.classList.remove("th-sort-asc", "th-sort-desc");
  });
  var targetTh = table.querySelector("th:nth-child(" + (column + 1) + ")");
  if (targetTh) {
    targetTh.classList.toggle("th-sort-asc", asc);
    targetTh.classList.toggle("th-sort-desc", !asc);
  }
}

// Column sort via header click
document.addEventListener('DOMContentLoaded', function() {
  document.querySelectorAll(".section-card th").forEach(function(headerCell) {
    headerCell.addEventListener("click", function() {
      var tableElement = headerCell.closest("table");
      if (!tableElement) return;
      var headerIndex = Array.prototype.indexOf.call(headerCell.parentElement.children, headerCell);
      var currentIsAscending = headerCell.classList.contains("th-sort-asc");
      sortTableByColumn(tableElement, headerIndex, !currentIsAscending);
    });
  });
});

// Backward-compatible sort function referenced by onclick="sortTable(N)" in headers
function sortTable(columnIndex) {
  // Find the table closest to the event target
  var el = event && event.target ? event.target : null;
  if (!el) return;
  var table = el.closest("table");
  if (!table) return;
  var isAsc = el.classList.contains("th-sort-asc");
  sortTableByColumn(table, columnIndex, !isAsc);
}

// ===== Scroll to Top =====
var mybutton = document.getElementById("myBtn");
window.onscroll = function() {
  if (mybutton) {
    if (document.body.scrollTop > 500 || document.documentElement.scrollTop > 500) {
      mybutton.style.display = "block";
    } else {
      mybutton.style.display = "none";
    }
  }
};

function topFunction() {
  document.body.scrollTop = 0;
  document.documentElement.scrollTop = 0;
}

// ===== Sidebar Active Link Tracking =====
document.addEventListener('DOMContentLoaded', function() {
  var sections = document.querySelectorAll('.section-card[id]');
  var navLinks = document.querySelectorAll('.nav-link');

  // Click handler: open section if collapsed
  navLinks.forEach(function(link) {
    link.addEventListener('click', function(e) {
      var targetId = this.getAttribute('href');
      if (!targetId) return;
      var targetSection = document.querySelector(targetId);
      if (targetSection && !targetSection.classList.contains('open')) {
        targetSection.classList.add('open');
      }
    });
  });

  // Scroll spy for active link
  if (sections.length > 0 && navLinks.length > 0) {
    var observer = new IntersectionObserver(function(entries) {
      entries.forEach(function(entry) {
        if (entry.isIntersecting) {
          navLinks.forEach(function(l) { l.classList.remove('active'); });
          var activeLink = document.querySelector('.nav-link[href="#' + entry.target.id + '"]');
          if (activeLink) activeLink.classList.add('active');
        }
      });
    }, { rootMargin: '-20% 0px -80% 0px' });

    sections.forEach(function(section) { observer.observe(section); });
  }
});
