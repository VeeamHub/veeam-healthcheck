var coll = document.getElementsByClassName("collapsible");
var navLink = document.getElementsByClassName("smoothscroll");
var i;
let expanded = false;

for (i = 0; i < coll.length; i++) {
	coll[i].addEventListener("click", function () {
		this.classList.toggle("active");
		var content = this.nextElementSibling;
		if (content.style.display === "block") {
			content.style.display = "none";
		} else {
			content.style.display = "block";
		}
	});
}


for (i = 0; i < navLink.length; i++) {
	navLink[i].addEventListener("click", function () {
		var link = this.dataset.link;
		var sectionId = document.getElementById(link);

		var divToOpen = sectionId.querySelector(".collapsible");

		divToOpen.classList.toggle("active");
		var content = divToOpen.nextElementSibling;
		if (content.style.display === "block") {
			content.style.display = "block";
		} else {
			content.style.display = "block";
		}
	});
}

let isExpanded = false;
function test() {
	var co = document.getElementsByClassName("collapsible");
	var divs = document.querySelectorAll(".collapsible");

	isExpanded = !isExpanded;

	const btnText = isExpanded ? "Collapse All Sections" : "Expand All Sections";
	document.getElementById("expandBtn").textContent = btnText;

	divs.forEach(d => {
		d.classList.toggle("active");
		var content = d.nextElementSibling;
		if (content.style.display === "block") {
			content.style.display = "none";
		} else {
			content.style.display = "block";
		}
	});
	//alert("The function 'test' is executed");

}
function sortTableByColumn(table, column, asc = true) {
	const dirModifier = asc ? 1 : -1;
	const tBody = table.tBodies[0];
	const rows = Array.from(tBody.querySelectorAll("tr"));
	const sortedRows = rows.sort((a, b) => {
		const aColText = a.querySelector(`td:nth-child(${column + 1})`).textContent.trim();
		const bColText = b.querySelector(`td:nth-child(${column + 1})`).textContent.trim();

		return aColText > bColText ? (1 * dirModifier) : (-1 * dirModifier);
	});

	// Remove all existing TRs from the table
	while (tBody.firstChild) {
		tBody.removeChild(tBody.firstChild);
	}

	// Re-add the newly sorted rows
	tBody.append(...sortedRows);

	// Remember how the column is currently sorted
	table.querySelectorAll("th").forEach(th => th.classList.remove("th-sort-asc", "th-sort-desc"));
	table.querySelector(`th:nth-child(${column + 1})`).classList.toggle("th-sort-asc", asc);
	table.querySelector(`th:nth-child(${column + 1})`).classList.toggle("th-sort-desc", !asc);
}

document.querySelectorAll(".table-sortable th").forEach(headerCell => {
	headerCell.addEventListener("click", () => {
		const tableElement = headerCell.parentElement.parentElement.parentElement;
		const headerIndex = Array.prototype.indexOf.call(headerCell.parentElement.children, headerCell);
		const currentIsAscending = headerCell.classList.contains("th-sort-asc");

		sortTableByColumn(tableElement, headerIndex, !currentIsAscending);
	});
});
// Get the button:
let mybutton = document.getElementById("myBtn");

// When the user scrolls down 20px from the top of the document, show the button
window.onscroll = function () { scrollFunction() };

function scrollFunction() {
	if (document.body.scrollTop > 500 || document.documentElement.scrollTop > 500) {
		mybutton.style.display = "block";
	} else {
		mybutton.style.display = "none";
	}
}

// When the user clicks on the button, scroll to the top of the document
function topFunction() {
	document.body.scrollTop = 0; // For Safari
	document.documentElement.scrollTop = 0; // For Chrome, Firefox, IE and Opera
}