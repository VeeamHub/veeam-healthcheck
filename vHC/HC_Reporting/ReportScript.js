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

// ===== Legacy Collapsible Toggle (for sections using SectionStartWithButton) =====
document.addEventListener('DOMContentLoaded', function() {
  document.querySelectorAll('.collapsible').forEach(function(btn) {
    btn.addEventListener('click', function() {
      var content = this.nextElementSibling;
      if (content && content.classList.contains('content')) {
        content.style.display = (content.style.display === 'none' || content.style.display === '') ? 'block' : 'none';
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
  var rows = Array.from(tBody.querySelectorAll("tr"));
  var sortedRows = rows.sort(function(a, b) {
    var aCell = a.querySelector("td:nth-child(" + (column + 1) + ")");
    var bCell = b.querySelector("td:nth-child(" + (column + 1) + ")");
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
  for (var i = 0; i < sortedRows.length; i++) { tBody.appendChild(sortedRows[i]); }
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
