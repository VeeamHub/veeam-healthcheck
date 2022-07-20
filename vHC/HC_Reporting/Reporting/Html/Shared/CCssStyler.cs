using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VeeamHealthCheck.Reporting.Html.Shared
{
    class CssStyler
    {
        public static string StyleString()
        {
            return "html *{\n" +
            "font-family: Tahoma !important;\n" +
            "}\n" +
            ".rhtitle {\n" +
            "color: green;\n" +
            "font-weight: bold;\n" +
            "font-size: 25px;\n" +
            "}\n" +
            ".i2 {\n" +
            "padding-left: 20px;\n" +
            "}\n" +
            "div.bulletlist{text-indent:20px}\n" +
            ".i3 {\n" +
            "padding-left: 40px;\n" +
            "}\n" +
            ".i4 {\n" +
            "padding-left: 60px;\n" +
            "}\n" +
            "\n" +
            ".hdr{\n" +
            "color: green;\n" +
            "font-style: italic;\n" +
            "font-weight: bold;\n" +
            "font-size: 20px;\n" +
            "}\n" +
            ".subhdr{\n" +
            "padding-left:10px;\n" +
            "color: green;\n" +
            "font-weight: bold;\n" +
            "font-size:15px;\n" +
            "}\n" +
            ".bld{\n" +
            "font-weight: bold;\n" +
            "}\n" +
            ".logo {\n" +
            "background-attachment: fixed;\n" +
            "background-position: center;\n" +
            "background-size: 20%;\n" +
            "display: block;\n" +
            "}\n" +
            ".subtext{\n" +
            "font-size:15px;\n" +
            "}\n" +
            ".collapsible{\n" +
            "background-color: #eee;\n" +
            "color: #444;\n" +
            "cursor: pointer;\n" +
            "padding: 18px;\n" +
            "width: 100%;\n" +
            "border: none;\n" +
            "text-align: left;\n" +
            "outline: none;\n" +
            "font-size: 15px;\n" +
            "}\n" +
            ".active, .collapsible:hover {\n" +
            "background-color: #ccc;\n" +
            "}\n" +
            ".content {\n" +
            "padding: 0 18px;\n" +
            "display: none;\n" +
            "overflow: scroll;\n" +
            "background-color: #f1f1f1;\n" +
            "transition: max-height 0.2s ease-out;\n" +
            "}\n" +
            ".th{\n" +
            "font-color: white;\n" +
            "}\n" +
            "th{\n" +
            "color: white;\n" +
            "background-color: #005f4b\n" +
            "}\n" +
            ".collapsible:after {\n" +
            "content: '\\02795'; /* Unicode character for \"plus\" sign (+) */\n" +
            "font-size: 13px;\n" +
            "color: white;\n" +
            "float: right;\n" +
            "margin-left: 5px;\n" +
            "}\n" +
                        ".active:after {\n" +
            "content: \"\\2796\"; /* Unicode character for \"minus\" sign (-) */\n" +
            "}\n" +
            ".btn{\n" +
            "color: white;\n" +
            "background-color: #1d6b5b\n" +
            "}\n" +
            ".btn:hover{\n" +
            "background-color: #54b948\n" +
            "}" +
            "div:not(#procstats,#navigation) table tr:nth-child(2n+1){" +
            "background-color: #dcf7ea;" +
            "}" +
            "#procstats tr:nth-child(12n+8)," +
            "#procstats tr:nth-child(12n+9)," +
            "#procstats tr:nth-child(12n+10)," +
            "#procstats tr:nth-child(12n+11)," +
            "#procstats tr:nth-child(12n+12)," +
            "#procstats tr:nth-child(12n+13) {" +
            "background-color: #dcf7ea;" +
            "}";




        }
        public static string JavaScriptBlock()
        {
            return "var coll = document.getElementsByClassName(\"collapsible\");\n" +
"var navLink = document.getElementsByClassName(\"smoothscroll\");\n" +
"var i;\n" +
"\n" +
"for (i = 0; i < coll.length; i++) {\n" +
"  coll[i].addEventListener(\"click\", function() {\n" +
"	this.classList.toggle(\"active\");\n" +
"	var content = this.nextElementSibling;\n" +
"	if (content.style.display === \"block\") {\n" +
"	  content.style.display = \"none\";\n" +
"	} else {\n" +
"	  content.style.display = \"block\";\n" +
"	}\n" +
"  });\n" +
"}\n" +
"\n" +
"\n" +
"for (i = 0; i < navLink.length; i++) {\n" +
"  navLink[i].addEventListener(\"click\", function() {\n" +
"	var link = this.dataset.link;\n" +
"	var sectionId = document.getElementById(link);\n" +
"\n" +
"	var divToOpen = sectionId.querySelector(\".collapsible\");\n" +
"	\n" +
"	divToOpen.classList.toggle(\"active\");\n" +
"	var content = divToOpen.nextElementSibling;\n" +
"	if (content.style.display === \"block\") {\n" +
"	  content.style.display = \"block\";\n" +
"	} else {\n" +
"	  content.style.display = \"block\";\n" +
"	}\n" +
"  });\n" +
"}\n" +
"\n" +
"\n" +
"function test(){\n" +
"    var co = document.getElementsByClassName(\"collapsible\");\n" +
"    var divs = document.querySelectorAll(\".collapsible\");\n" +
"    \n" +
"    divs.forEach(d => {\n" +
"        d.classList.toggle(\"active\");\n" +
"        var content = d.nextElementSibling;\n" +
"        if(content.style.display === \"block\"){\n" +
"            content.style.display = \"none\";\n" +
"        } else{\n" +
"            content.style.display = \"block\";\n" +
"        }\n" +
"    });\n" +
"//alert(\"The function 'test' is executed\");\n" +
"\n" +
"}";




        }
    }
}
