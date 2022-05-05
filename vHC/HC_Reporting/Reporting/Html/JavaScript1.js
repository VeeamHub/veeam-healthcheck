





var coll = document.getElementsByClassName("collapsible");
var navLink = document.getElementsByClassName("smoothscroll");
var i;

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


function test() {
	var co = document.getElementsByClassName("collapsible");
	var divs = document.querySelectorAll(".collapsible");

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




