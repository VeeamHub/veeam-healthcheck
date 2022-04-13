<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0"
				xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
				xmlns:msxsl="urn:schemas-microsoft-com:xslt"
				
>
	<xsl:output method="html" indent="yes" />
	<xsl:strip-space elements="*"/>

	<xsl:template match="/">

		<style>
			<!--@top-right {
			content: url("Veeam_HealthCheck_228x228.png");
			width: 49.634px;
			height: 49.449px;
			image-resolution: 480dpi;
			padding-top: 0in;
			}-->
			html *{
			font-family: Tahoma !important;
			}
			.rhtitle {
			color: green;
			font-weight: bold;
			font-size: 25px;
			}
			.i2 {
			padding-left: 20px;
			}
			div.bulletlist{text-indent:20px}
			.i3 {
			padding-left: 40px;
			}
			.i4 {
			padding-left: 100px;
			}

			.hdr{
			color: green;
			font-style: italic;
			font-weight: bold;
			font-size: 20px;
			}
			.subhdr{
			padding-left:10px;
			color: green;
			font-weight: bold;
			font-size:15px;
			}
			.bld{
			font-weight: bold;
			}
			.logo {
			background-attachment: fixed;
			background-position: center;
			background-size: 20%;
			display: block;
			}
			.subtext{
			font-size:15px;
			}
			.collapsible{
			background-color: #eee;
			color: #444;
			cursor: pointer;
			padding: 18px;
			width: 100%;
			border: none;
			text-align: left;
			outline: none;
			font-size: 15px;
			}
			.active, .collapsible:hover {
			background-color: #ccc;
			}
			.content {
			padding: 0 18px;
			display: none;
			overflow: hidden;
			background-color: #f1f1f1;
			transition: max-height 0.2s ease-out;
			}
			.th{
			font-color: white;
			}
			th{
			color: white;
			background-color: #005f4b
			}
			.collapsible:after {
			content: '\02795'; /* Unicode character for "plus" sign (+) */
			font-size: 13px;
			color: white;
			float: right;
			margin-left: 5px;
			}

			.active:after {
			content: "\2796"; /* Unicode character for "minus" sign (-) */
			}
			.btn{
			color: white;
			background-color: #1d6b5b
			}
			.btn:hover{
			background-color: #54b948
			}

		</style>
		<html>

			<head>
				<xsl:apply-templates select="/root/header"></xsl:apply-templates>
			</head>
			<body>
				<!--EXPANDED-->
				<xsl:apply-templates select="/root/license"></xsl:apply-templates>
				<xsl:apply-templates select="/root/backupServer"></xsl:apply-templates>
				<xsl:apply-templates select="/root/secSummary"></xsl:apply-templates>
				<xsl:apply-templates select="/root/serverSummary"></xsl:apply-templates>
				<xsl:apply-templates select="/root/jobSummary"></xsl:apply-templates>

				<!--NOT-EXPANDED-->
				<xsl:apply-templates select="/root/npjobSummary"></xsl:apply-templates>
				<xsl:apply-templates select="/root/protectedWorkloads"></xsl:apply-templates>
				<xsl:apply-templates select="/root/servers"></xsl:apply-templates>
				<xsl:apply-templates select="/root/regOptions"></xsl:apply-templates>
				<xsl:apply-templates select="/root/proxies"></xsl:apply-templates>
				<xsl:apply-templates select="/root/sobrs"></xsl:apply-templates>
				<xsl:apply-templates select="/root/extent"></xsl:apply-templates>
				<xsl:apply-templates select="/root/repositories"></xsl:apply-templates>
				<xsl:apply-templates select="/root/concurrencyChart_job7"></xsl:apply-templates>
				<xsl:apply-templates select="/root/concurrencyChart_task7"></xsl:apply-templates>
				<xsl:apply-templates select="/root/jobSessionsSummary"></xsl:apply-templates>
				<xsl:apply-templates select="/root/jobs"></xsl:apply-templates>
				<xsl:apply-templates select="/root/jobSessions"></xsl:apply-templates>
				<xsl:apply-templates select="/root/footer"></xsl:apply-templates>

				<!--m365-->
				<xsl:apply-templates select="/root/Global"></xsl:apply-templates>
				<!--<script type="text/javascript" src="JavaScript1.js"></script>-->

				<script type="text/javascript">
					<xsl:text disable-output-escaping="yes" >
			<![CDATA[




var coll = document.getElementsByClassName("collapsible");
var navLink = document.getElementsByClassName("smoothscroll");
var i;

for (i = 0; i < coll.length; i++) {
  coll[i].addEventListener("click", function() {
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
  navLink[i].addEventListener("click", function() {
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


function test(){
    var co = document.getElementsByClassName("collapsible");
    var divs = document.querySelectorAll(".collapsible");
    
    divs.forEach(d => {
        d.classList.toggle("active");
        var content = d.nextElementSibling;
        if(content.style.display === "block"){
            content.style.display = "none";
        } else{
            content.style.display = "block";
        }
    });
//alert("The function 'test' is executed");

}



		]]>
		</xsl:text>
				</script>

			</body>
		</html>
	</xsl:template>
				<xsl:param name="lang">en</xsl:param>
	
	<xsl:template name="user.header.content"  match="/root/header">
		<html>
			<body>
				<div id="top"></div>
				<HR/>
				<h1 style="background: #005f4b; color: white; font-family: tahoma;text-align:center">
					<a class="logo">
						<img src="data:image/png;base64,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" />
					</a>
					<xsl:apply-templates select="//h1/name" mode="titlepage.mode" />
					<br></br>
					<h2 style="color: green; font-style: italic;background:lightgray;text-align:center">
						<!--<xsl:apply-templates select="//h1/hc"></xsl:apply-templates>-->
						<xsl:value-of select="summaryheader"/>
					</h2>
					<a class="GenText">
						<xsl:for-each select="text">
							<xsl:if test="text/@indent='hdr'">
								<xsl:attribute name="class">
									<xsl:text>hdr</xsl:text>
								</xsl:attribute>
							</xsl:if>
							<xsl:if test="text/@indent='i2'">
								<xsl:attribute name="class">
									i2
								</xsl:attribute>
							</xsl:if>
							<div class="{@indent}">
								<xsl:value-of select ="."/>
							</div>
						</xsl:for-each>
						
					</a>
				
				</h1>

				<!--COMMENTING OUT NAVIGATION TEMPORARILY WHILE TESTING THE NEW COLLAPSIBLE LAYOUT-->

				<h4>
					Navigation:<br></br>
					<button type="button" class="btn" onclick="test()">
						Collapse/Expand All
					</button>
					<table border="0">
						<tr>
							<td>
								<li>
									<a class="smoothscroll" data-link="license" href="#license" >License Info</a>
								</li>
							</td>
							<td>Displays license information.</td>
						</tr>
						<tr>
							<td>
								<li>
									<a class="smoothscroll" data-link="backupserver" href="#backupserver" >Backup Server</a>
								</li>
							</td>
							<td>Detailed information on the Backup Server, Configuration DB, roles, and resources.</td>
						</tr>
						<tr>
							<td>
								<li>
									<a class="smoothscroll" data-link="secSummary" href="#secSummary" >Security Summary</a>
								</li>
							</td>
							<td>Brief table showing security features in use.</td>
						</tr>
						<tr>
							<td>
								<li>
									<a class="smoothscroll" data-link="serverSummary" href="#serverSummary" >Server Summary</a>
								</li>
							</td>
							<td>Summary of detected infrastructure types and count.</td>
						</tr>
						<tr>
							<td>
								<li>
									<a class="smoothscroll" data-link="linkjobSummary" href="#linkjobSummary">Job Summary</a>
								</li>
							</td>
							<td>
								Summary of found jobs by type and count.
							</td>
						</tr>
						<tr>
							<td>
								<li>
									<a class="smoothscroll" data-link="npJobSummary" href="#npJobSummary" >Missing Job Types</a>
								</li>
							</td>
							<td>Displays workloads and types that are not utilized.</td>
						</tr>
						<tr>
							<td>
								<li>
									<a class="smoothscroll" data-link="protectedWorkloads" href="#protectedWorkloads" >Protected Workloads</a>
								</li>
							</td>
							<td>Shows count of detected VM &amp; Physical objects compared to objects with backups.</td>
						</tr>
						<tr>
							<td>
								<li>
									<a class="smoothscroll" data-link="linkserverList" href="#linkserverList">Server Info</a>
								</li>
							</td>
							<td>
								List of all servers added into the Backup Console.
							</td>
						</tr>
						<tr>
							<td>
								<li>
									<a class="smoothscroll" data-link="linkregkeys" href="#linkregkeys">Non-Default Registry Keys</a>
								</li>
							</td>
							<td>Shows any registry values differing from a vanilla install.</td>

						</tr>
						<tr>
							<td>
								<li>
									<a class="smoothscroll" data-link="linkproxy" href="#linkproxy">Proxy Info</a>
								</li>
							</td>
							<td>
								Details on proxy resources and config.
							</td>
						</tr>
						<tr>
							<td>
								<li>
									<a class="smoothscroll" data-link="linksobrInfo" href="#linksobrInfo">SOBR Info</a>
								</li>
							</td>
							<td>Summary of SOBRs and config options.</td>
						</tr>
						<tr>
							<td>
								<li>
									<a class="smoothscroll" data-link="linksobrExt" href="#linksobrExt">SOBR Extent Info</a>
								</li>
							</td>
							<td>Details on SOBR Extents, resources, and config</td>
						</tr>
						<tr>
							<td>
								<li>
									<a class="smoothscroll" data-link="linkrepo" href="#linkrepo">Repository Info</a>
								</li>
							</td>
							<td>
								Details on non-SOBR repositories, resources, and config.
							</td>
						</tr>



						<tr>
							<td>
								<li>
									<a class="smoothscroll" data-link="linkjobConcurrency7" href="#linkjobConcurrency7">Job Concurrency</a>
								</li>
							</td>
							<td>
								Heatmap showing max job concurrency found per hour.
							</td>
						</tr>
						<tr>
							<td>
								<li>
									<a class="smoothscroll" data-link="linktaskConcurrency7" href="#linktaskConcurrency7">VM Concurrency</a>
								</li>
							</td>
							<td>
								Heatmap showing max VM concurrency found per hour.
							</td>
						</tr>

						<tr>
							<td>
								<li>
									<a class="smoothscroll" data-link="linkt" href="#linkt">Job Session Summary</a>
								</li>
							</td>
							<td>Summary of job sessions by job name, item count, and min/max/avg time.</td>
						</tr>
						<tr>
							<td>
								<li>
									<a class="smoothscroll" data-link="linkjobs" href="#linkjobs">Job Info</a>
								</li>
							</td>
							<td>Detailed breakdown of each job.</td>
						</tr>

						<!--<tr>
							<td>
								<a href="#sessions">
									Job Session Info
								</a>
							</td>
							<td>Detailed breakdown of each job session.</td>
						</tr>-->

					</table>

				</h4>
			</body>
		</html>

	</xsl:template>

	<xsl:template match="/root/license" >
		<div id="license">
			<h2>
				<u>License Summary</u>
			</h2>
		</div>
		<html>
			<body>
				<table border="1" style="background: lightgray">
					<tr>
						<th title="Veeam License Edition">Edition</th>
						<th title="Veeam Licensing Status">Status</th>
						<th title="Veeam License Type">Type</th>
						<th title="Total Licensed Instances">Licensed Inst.</th>
						<th title="Total Used Instances">Used Inst.</th>
						<th title="New Instances">New Inst.</th>
						<th title="Rental Instances">Rental Inst.</th>
						<th title="Total Licensed Sockets">Licensed Sockets</th>
						<th title="Total Used Sockets">Used Sockets</th>
						<th title="Total Licensed NAS">Licensed NAS</th>
						<th title="Total Used NAS">Used NAS</th>
						<th title="Veeam License Expiration Date">Exp Date</th>
						<th title="Veeam Support Expiration Date">Support Exp Date</th>
						<th title="Is Cloud Connected Enabled?">Cloud Connect Enabled</th>
					</tr>
					<xsl:for-each select="licInfo">
						<tr>
							<td title="">
								<xsl:value-of select="edition"/>
							</td>
							<td title="">
								<xsl:value-of select="status"/>
							</td>
							<td title="">
								<xsl:value-of select="type"/>
							</td>
							<td title="" style="text-align:right">
								<xsl:value-of select="licInst"/>
							</td>
							<td title="" style="text-align:right">
								<xsl:value-of select="usedInst"/>
							</td>
							<td title="" style="text-align:right">
								<xsl:value-of select="newInst"/>
							</td>
							<td title="" style="text-align:right">
								<xsl:value-of select="rentInst"/>
							</td>
							<td title="" style="text-align:right">
								<xsl:value-of select="licSock"/>
							</td>
							<td title="" style="text-align:right">
								<xsl:value-of select="usedSock"/>
							</td>
							<td title="" style="text-align:right">
								<xsl:value-of select="licCap"/>
							</td>
							<td title="" style="text-align:right">
								<xsl:value-of select="usedCap"/>
							</td>
							<td title="" style="text-align:right">
								<xsl:value-of select="expire"/>
							</td>
							<td title="" style="text-align:right">
								<xsl:value-of select="supExp"/>
							</td>
							<td title="" style="text-align:right">
								<xsl:value-of select="cloudconnect"/>
							</td>
						</tr>
					</xsl:for-each>
				</table>

				<h5>
					<a href="#top">Back To Top</a>
				</h5>
			</body>
		</html>

	</xsl:template>
	<xsl:template match="/root/npjobSummary" name="npLoads">
		<div id="npJobSummary">
			<h2>
				<u>Missing Job Types</u>
			</h2>
			<button type="button" class="collapsible">Show details on missing job types</button>
			<div class="content">
				<br></br>
				<table border="1" title="Unprotected Workloads">
					<tr>
						<th>
							<xsl:value-of select="td/@headerName"/>
						</th>
						<th>Count</th>
					</tr>
					<xsl:for-each select="td">
						<tr>
							<td title="" style="text-align:left">
								<xsl:value-of select="current()"/>
							</td>
							<td>0</td>
						</tr>
					</xsl:for-each>
				</table>
				<div class="hdr">Summary:</div>
				<br></br>
				<div class="">This table summarizes job types that are either missing from your configuration or could not be analyzed. Please consult with your Veeam Engineer for more information.</div>
			</div>
		</div>

	</xsl:template>
	<xsl:template match="/root/protectedWorkloads" name="pLoads">
		<div id="protectedWorkloads">
			<h2>
				<u>Protected Workloads</u>
			</h2>
			<button type="button" class="collapsible">Show count of protected workloads</button>
			<div class="content">
				<br></br>
				<table border="1" title="Protected Workloads">
					<tr>
						<!--<xsl:for-each select="td">
							<th>
								<xsl:value-of select="current()/@headerName"/>
							</th>	
						</xsl:for-each>-->
						<th title="Total VMware VMs found.">Vi Total</th>
						<th title="Total VMware VMs found with backups">Vi Protected</th>
						<th title="Total VMwar VMs found with no backups">Vi Not Prot</th>
						<th title="Potentially duplicated workloads">Vi Potential Dupes</th>
						<th title="VMs backed up via Agents as physical workloads">Vi Protected as physical</th>
						<th title="Total count of servers added into Protection Groups">Phys Total</th>
						<th title="Total count of servers with backups">Phys Protected</th>
						<th title="Total count of servers in a Protection Group but with no current backups">Phys Not Prot</th>
						<!--<th title=""></th>-->

					</tr>
					<tr>
						<xsl:for-each select="td">
							<td title="" style="text-align:left">
								<xsl:value-of select="current()"/>
							</td>
						</xsl:for-each>
					</tr>
				</table>
				<div class="hdr">Summary:</div>
				<br></br>
				<div class="">This table summarizes the amount of workloads detected in the current backup server. The VMware (Vi Total) count is determined by the local Veeam Broker service and compares that VM count with the existing backups. The physical workloads (Phys Total) are determined by the number of VMs added to Protection Groups and then compared to what is in backup jobs.</div>
				<br></br>
				<div class="hdr">Notes:</div>
				<br></br>
				<div class="i2">
					"Duplicates" shows the discrepancy between unique workloads in backup jobs and those workloads across multiple backup jobs as may be reflected in the VMC file. It does not mean VM's (or their license consumption) is being duplicated.
				</div>
			</div>
		</div>

	</xsl:template>
	<xsl:template match="/root/secSummary">
		<div id ="secSummary">
			<h2>
				<u>Security Summary</u>
			</h2>
		</div>

		<html>
			<body>

				<table border="1">
					<tr>
						<div class ="th">
							<th title="">Immutability</th>
							<th title="">Traffic Encryption</th>
							<th>Backup File Encryption</th>
							<th>Config Backup Encryption</th>
						</div>
					</tr>
					<xsl:for-each select="secProfile">
						<tr>
							<td title="" style="text-align:right">
								<xsl:if test="immute='false'">
									<xsl:attribute name="bgcolor">
										<xsl:text>orangered</xsl:text>

									</xsl:attribute>
								</xsl:if>
								<xsl:value-of select="immute"/>
							</td>
							<td title="" style="text-align:right">
								<xsl:if test="trafficInc='false'">
									<xsl:attribute name="bgcolor">
										<xsl:text>orangered</xsl:text>
									</xsl:attribute>
								</xsl:if>
								<xsl:value-of select="trafficEnc"/>
							</td>
							<td title="" style="text-align:right">
								<xsl:if test="backupEnc='false'">
									<xsl:attribute name="bgcolor">
										<xsl:text>orangered</xsl:text>
									</xsl:attribute>
								</xsl:if>
								<xsl:value-of select="backupEnc"/>
							</td>
							<td title="" style="text-align:right">
								<xsl:if test="configEnc='false'">
									<xsl:attribute name="bgcolor">
										<xsl:text>orangered</xsl:text>
									</xsl:attribute>
								</xsl:if>
								<xsl:value-of select="configEnc"/>
							</td>
						</tr>
					</xsl:for-each>
				</table>

				<button type="button" class="collapsible">Show details for security summary.</button>
				<div class="content">
					<br></br>
					<div class="hdr">Summary:</div>
					<br></br>
					<div class="">The Security Summary Table provides an at-a-glance view of which security features are enabled on one or more components within your B&amp;R Configuration. A value of "false" may be an area for remediation.</div>
					<br></br>
					<div class="hdr">Notes:</div>
					<br></br>
					<div class="subhdr">Immutability</div>
					<div class="i2">
						For immutable backups, consider deploying a <a href="https://helpcenter.veeam.com/docs/backup/vsphere/hardened_repository.html?ver=110">Hardened Linux Repository</a> or choose a <a href="https://helpcenter.veeam.com/docs/backup/vsphere/object_storage_repository.html?ver=110">Public Cloud or S3-Compatible</a> option that offers immutability.
					</div>
					<br></br>
					<div class="subhdr">Traffic Encryption</div>
					<div class="i2">
						<a href="https://helpcenter.veeam.com/docs/backup/vsphere/security_considerations.html?zoom_highlight=traffic+encryption&amp;ver=110#:~:text=Encrypt%20network%20traffic,Network%20Data%20Encryption.">Traffic Encryption</a> is enabled for all public traffic by default. If this value shows as false, consider re-enabling the setting for Internet or any other potentially sensitive transfers.
					</div>
					<br></br>
					<div class="subhdr">Backup File Encryption</div>
					<div class="i2">
						Backup File Encryption prevents outsiders from importing your backups and having access to the contents. If backups are stored offsite, consider <a href="https://helpcenter.veeam.com/docs/backup/vsphere/backup_job_advanced_storage_vm.html?ver=110#:~:text=To%20encrypt%20the%20content%20of%20backup%20files%2C%20select%20the%20Enable%20backup%20file%20encryption%20check%20box.">enabling</a> Backup File Encryption to protect the backups from unwanted access.
					</div>
					<br></br>
					<div class="subhdr">Config Backup Encryption</div>
					<div class="i2">
						The <a href="https://helpcenter.veeam.com/docs/backup/vsphere/vbr_config.html?ver=110">Configuration Backup</a> ensures your B&amp;R configuration is backed up and ready to restore in the event of an outage or migration.
						Having the config backup <a href="https://helpcenter.veeam.com/docs/backup/vsphere/config_backup_encrypted.html?ver=110">encrypted</a> allows protection from unwanted access as well as the ability to restore the passwords previously stored inside the configuration.
						<a style="font-weight:bold">Consider using Backup Enterprise Manager's </a>
						<a href="https://helpcenter.veeam.com/docs/backup/em/em_manage_keys.html?ver=110">password protection</a>
						<a style="font-weight:bold"> to ensure lost passwords are recoverable, and separating Enterprise Manager and VBR in the event of a catastrophic outage. Also consider exporting the keyset to a secure location. Without this protection, lost passwords can cripple the ability to use backup files.</a>
						<div class=""></div>
						<div class=""></div>
						<div class=""></div>
						<h5>
							<a href="#top">Back To Top</a>
						</h5>
					</div>
				</div>
			</body>
		</html>

	</xsl:template>
	<!--<xsl:template name="table">
		<table border="1" title="TEST TABLE">
			<tr >
				<th>
					<xsl:value-of select="td/@headerName"/>
				</th>
				<th>Count</th>
			</tr>
			<xsl:for-each select="td">
				<tr>
					<td title="" style="text-align:left">
						<xsl:value-of select="current()"/>
					</td>
					<td>0</td>
				</tr>
			</xsl:for-each>
		</table>
	</xsl:template>-->
	<xsl:template match="/root/serverSummary">
		<div id="serverSummary">
			<h2>
				<u>Detected Infrastructure Types &amp; Count</u>
			</h2>
		</div>
		<html>
			<body>

				<table border="1">
					<tr >
						<th title="">Name</th>
						<th title="">Count</th>
					</tr>
					<xsl:for-each select="server">
						<tr>
							<td title="Name">
								<xsl:value-of select="name"/>
							</td>
							<td title="Total Detected Count" style="text-align:right">
								<xsl:value-of select="count"/>
							</td>
						</tr>
					</xsl:for-each>
				</table>
				<button type="button" class="collapsible">Show details for detected infrastructure types &amp; counts</button>
				<div class="content">
					<div class="hdr">Summary:</div>
					<br></br>
					<div class="i2">This table summarizes the different types and counts of infrastructure items that are manually added into the Veeam configuration by the user. </div>
					<!--<br></br>
					<div class="hdr">Notes:</div>
					<br></br>
					<div class="i2">
						•
					</div>-->
					<h5>
						<a href="#top">Back To Top</a>
					</h5>
				</div>
			</body>
		</html>
	</xsl:template>
	<xsl:template match="/root/backupServer">
		<div id="backupserver">
			<h2>
				<u>Backup Server &amp; Config DB Info</u>
			</h2>
		</div>

		<html>
			<body>
				<table border="1">
					<tr >
						<th>Name</th>
						<th>Veeam Version</th>
						<th>Cores</th>
						<th>RAM</th>
						<th>Config Backup Enabled</th>
						<th>Config Backup Last Result</th>
						<th>Config Backup Encryption</th>
						<th>Config Backup Target</th>
						<th>Local SQL</th>
						<th>SQL Server Name</th>
						<th>SQL Version</th>
						<th>SQL Edition</th>
						<th>SQL Cores</th>
						<th>SQL RAM</th>
						<th>Proxy Role</th>
						<th>Repo/Gateway</th>
						<th>WAN Acc.</th>
					</tr>
					<xsl:for-each select="serverInfo">
						<tr>
							<td>
								<xsl:value-of select="name"/>
							</td>
							<td style="text-align:right">
								<xsl:value-of select="veeamVersion"/>
							</td>
							<td style="text-align:right">
								<xsl:value-of select="cores"/>
							</td>
							<td style="text-align:right">
								<xsl:value-of select="ram"/>
							</td>
							<td>
								<xsl:value-of select="configBackupEnabled"/>
							</td>
							<td>
								<xsl:value-of select="configBackupLastResult"/>
							</td>
							<td>
								<xsl:value-of select="configBackupEncryption"/>
							</td>
							<td>
								<xsl:value-of select="configBackupTarget"/>
							</td>
							<td style="text-align:left">
								<xsl:value-of select="localSql"/>
							</td>
							<td>
								<xsl:value-of select="sqlname"/>
							</td>
							<td>
								<xsl:value-of select="sqlversion"/>
							</td>
							<td>
								<xsl:value-of select="sqledition"/>
							</td>
							<td style="text-align:right">
								<xsl:value-of select="sqlcpu"/>
							</td>
							<td style="text-align:right">
								<xsl:value-of select="sqlram"/>
							</td>
							<td style="text-align:right">
								<xsl:value-of select="proxyrole"/>
							</td>
							<td style="text-align:right">
								<xsl:value-of select="repo"/>
							</td>
							<td style="text-align:right">
								<xsl:value-of select="wanacc"/>
							</td>
						</tr>
					</xsl:for-each>
				</table>
				<button type="button" class="collapsible">Show backup server summary and notes.</button>
				<div class="content">
					<br></br>
					<div class="hdr">Summary:</div>
					<br></br>
					<div class="i2">The Backup Server (aka VBS or VBR Server) is the core component in the backup infrastructure that fills the role of the “configuration and control center”. The backup server performs all types of administrative activities, including: coordinating data protection &amp; recovery tasks, task scheduling &amp; resource allocation, and managing other backup infrastructure components.</div>
					<div class="i3">•	Backup Servers run on Microsoft Windows</div>
					<div class="i3">•	Stores data about the backup infrastructure, jobs, sessions, and other configuration data in either a local or remote SQL server. In a default installation, SQL Server Express is installed locally.</div>
					<div class="i3">•	Has additional components deployed by default: Proxy, Repository</div>
					<br></br>
					<div class="hdr">Notes:</div>
					<br></br>
					<div class="i2">
						•	Ensure resource <a href="https://bp.veeam.com/vbr/3_Build_structures/B_Veeam_Components/B_VBR_Server/Backup_Server.html#compute-requirements">sizing</a> of the Backup Server are adequate for the amount of concurrently running jobs that it manages. See <a href="#jobConcurrency7">Concurrency Table</a> in this report. Note: if combining other roles or components onto the Backup Server host, resource sizing is additive (e.g., Backup Server + SQL + Proxy + Repository, etc.).
					</div>
					<div class="i2">
						•	Ensure resource <a href="https://bp.veeam.com/vbr/2_Design_Structures/D_Veeam_Components/D_VBR_DB/database.html#sizing">sizing</a> of the SQL server are adequate
					</div>
					<div class="i2">
						•	If using SQL Express, ensure it is not breaching the SQL Express
						<a href="https://bp.veeam.com/vbr/2_Design_Structures/D_Veeam_Components/D_VBR_DB/database.html#sql-server-edition">hard-coded limits</a>
						(e.g., # of CPUs, cores, memory, and database capacity). A slow, non-responsive, or error-laden backup server can often be a result of reaching these SQL limits.
					</div>
					<div class="i2">•	Assigning data protection tasks to use the Default Proxy or Default Backup Repository components can lead to contention for backup server resources or other unintended traffic. Disabling, removing, and/or reassigning these away from the Backup Server may be desired in larger or distributed deployments.</div>
					<div class="i2">
						•	Fast-performing disk is recommended for the <a href="https://bp.veeam.com/vbr/3_Build_structures/B_Veeam_Components/B_VBR_Server/Backup_Server.html#log-files">log location</a>, which by default is "%ProgramData%\Veeam\Backup"
					</div>
					<div class="i2">
						•	DNS is a key component. Ensure
						<a href="https://bp.veeam.com/vbr/3_Build_structures/B_Other/dns_resolution.html#dns-resolution">DNS resolution</a>
						both from the Backup Server, and for other systems to the Backup Server is functioning correctly. Other components in the environment should be able to resolve the Backup Server and other Veeam infrastructure components (e.g., Proxy, Repository) by FQDN. Both forward and reverse queries should be functional.
					</div>
					<h5>
						<a href="#top">Back To Top</a>
					</h5>
				</div>
			</body>
		</html>
	</xsl:template>

	<xsl:template match="/root/sobrs">
		<div id="linksobrInfo">
			<h2>
				<u>SOBR Details</u>
			</h2>
			<button type="button" class="collapsible">Show SOBR Details</button>

			<div class="content">
				<html>
					<body>
						<!--<h3>
						SOBR Details
					</h3>-->
						<table border="1">
							<tr >
								<th title="SOBR Name">Name</th>
								<th title="Extent Count">Extents</th>
								<th title="Total jobs pointing to the SOBR">JobCount</th>
								<th title="Data policy">Policy</th>
								<th title="Is Capacity Tier enabled?">Capacity Tier</th>
								<th title="If Capacity Tier, is Copy Mode enabled?">Copy</th>
								<th title="If Capacity Tier, is Move Mode enabled?">Move</th>
								<th title="Is Archive Tier enabled?">Archive Tier</th>
								<th title="Does the SOBR use Per-Machine backup files?">Per-Machine</th>
								<th title="CapTier Type">CapTier Type</th>
								<th title="Immutable">Immutable</th>
								<th title="Immutable Period">Immutable Period</th>
								<th title="Size Limit Enabled">Size Limit Enabled</th>
								<th title="Size Limit">Size Limit</th>
								<!--<th title="Are Plugin Backups offloaded?">Plugin Backups Offload</th>
						<th title="">Copy All Machine Backups</th>-->
								<!--<th title="">Cost Optimized</th>-->
							</tr>
							<xsl:for-each select="sobr">
								<tr>
									<td title="Name">
										<xsl:value-of select="name"/>
									</td>
									<td title="Extent Count" style="text-align:right">
										<xsl:value-of select="extentCount"/>
									</td>
									<td title="Job Count" style="text-align:right">
										<xsl:value-of select="jobcount"/>
									</td>
									<td title="Policy" style="text-align:right">
										<xsl:value-of select="policy"/>
									</td>
									<td title="Capacity Tier enabled?" style="text-align:right">
										<xsl:value-of select="captier"/>
									</td>

									<td title="Copy Mode enabled?" style="text-align:right">
										<xsl:value-of select="copy"/>
									</td>
									<td title="Move Mode enabled?" style="text-align:right">
										<xsl:value-of select="move"/>
									</td>
									<td title="Archive Tier enabled?" style="text-align:right">
										<xsl:value-of select="archiveenabled"/>
									</td>
									<td title="Per-VM" style="text-align:right">
										<xsl:value-of select="pervm"/>
									</td>
									<td title="" style="text-align:right">
										<xsl:value-of select="capacitytext"/>
									</td>
									<td title="" style="text-align:right">
										<xsl:value-of select="immuteEnabled"/>
									</td>
									<td title="" style="text-align:right">
										<xsl:value-of select="immutePeriod"/>
									</td>
									<td title="" style="text-align:right">
										<xsl:value-of select="sizeLimitEnabled"/>
									</td>
									<td title="" style="text-align:right">
										<xsl:value-of select="sizeLimit"/>
									</td>
									<!--<td title="Plugin Offload" style="text-align:right">
								<xsl:value-of select="plugin"/>
							</td>
							<td title="" style="text-align:right">
								<xsl:value-of select="copyallmachine"/>
							</td>-->
									<!--<td title="" style="text-align:right">
				<xsl:value-of select="costoptimized"/>
			  </td>-->
								</tr>
							</xsl:for-each>
						</table>
						<br></br>
						<div class="hdr">Role Summary:</div>
						<div class="i2">o	A scale-out backup repository ("SOBR") is a single logical repository namespace that consists of one or more underlying backup repositories, or "extents", enabling horizontal scaling across multiple tiers. Data lands on the SOBR's local Performance tier(s), which can be extended with object storage tiers for long-term and archive storage: capacity tier and archive tier.</div>
						<div class="i3">•	SOBR can include multiple types of underlying extents, including: Window, Linux, NFS, SMB, Deduplication Appliances, and Object Storage</div>
						<div class="hdr">Notes:</div>
						<div class="i2">•	How many extents are in use? A large number of underlying extents can potentially lead to metadata management challenges and performance degradation. Discuss with your Veeam Engineer whether your extent count is satisfactory.</div>
						<div class="i2">
							•	Is the "Performance" <a href="https://helpcenter.veeam.com/docs/backup/vsphere/backup_repository_sobr_placement.html?ver=110">Placement Policy</a> in use? If so, consider whether there was a specific reason or use case? "Data Locality" Placement Policy is more often the ideal policy for most deployments and use cases.
						</div>
						<div class="i2">
							•	If using ReFS or XFS <a href="https://helpcenter.veeam.com/docs/backup/vsphere/backup_repository_block_cloning.html?ver=110">block cloning</a>, the "Data Locality" placement policy is required in order to leverage block cloning ("Fast clone") functionality.
						</div>
						<div class="i2">
							•	Is a <a href="https://helpcenter.veeam.com/docs/backup/hyperv/compatible_repository_account.html?ver=110"> gateway server in use for capacity tier copy/offload</a>? Ensure that the gateway server has adequate resources to act as the funnel for all backups sent to this object storage repository.
						</div>
						<div class="i2">
							•	The "<a href="https://helpcenter.veeam.com/docs/backup/hyperv/sobr_add_extents.html?ver=110">Perform full backup when the required extent is offline</a>" option (configured in the VBR console) should be considered carefully. It ensures that backups occur when the expected extent is offline, but with the trade off of additional space consumed for additional Active Full(s), and possible loss of storage efficiencies from volume-based block sharing technologies such as ReFS &amp; XFS.
						</div>
						<div class="i2">
							•	Is "<a href="https://helpcenter.veeam.com/docs/backup/vsphere/per_vm_backup_files.html?ver=110">Per-Machine</a>" false? If so, consider whether there was a specific reason or use case for disabling it? Enabling this is the ideal policy for most deployments and use cases. Consult with your Veeam Engineer.
						</div>
						<div class="i2">
							•	Is Infrequent Access storage tier used in conjunction with Veeam copy mode or Immutability on object storage? There are <a href="https://forums.veeam.com/object-storage-f52/aws-s3-how-to-reduce-the-number-of-api-calls-t68858.html#p382566">additional API calls associated with these processes</a> that should be considered when budgeting access costs.
						</div>
						<h5>
							<a href="#top">Back To Top</a>
						</h5>
					</body>
				</html>
			</div>
		</div>

	</xsl:template>
	<xsl:template match="extent">
		<div id="linksobrExt">
			<h2>
				<u>SOBR Extent Details</u>
			</h2>
			<button type="button" class="collapsible">Show SOBR Extent Details</button>
			<div class="content">
				<html>
					<body>
						<!--<h3>SOBR Extent Details</h3>-->
						<table border="1">
							<tr >
								<th title="Repository Name" style="text-align:left">Name</th>
								<th title="SOBR Name" style="text-align:left">SOBR</th>
								<th title="Tasks set for repository (-1 = unlimited)" style="text-align:left">Set Tasks</th>
								<th title="CPU Cores detected" style="text-align:left">Cores</th>
								<th title="Installed RAM detected" style="text-align:left">RAM(GB)</th>
								<th title="Does the repository use Automatic Gateway or dedicated?" style="text-align:left">AutoGateway</th>
								<th title="Host/Gateway Assigned if not Automatic Gateway" style="text-align:left">Host</th>
								<th title="Repository Path" style="text-align:left">Path</th>
								<th title="Detected Free Space based on last cached scan" style="text-align:left">FreeSpace(TB)</th>
								<th title="Detected Capacity of repository" style="text-align:left">TotalSpace(TB)</th>
								<th title="Calculated free space based on free space divided by total space" style="text-align:left">FreeSpace %</th>
								<!--<th title="Does repository use Per-VM files?" styel="text-aligh:left">Per-VM</th>-->
								<th title="Does repository decompress data before writing to storage?" style="text-align:left">DeCompress</th>
								<th title="Does the repository align data blocks?" style="text-align:left">AlignBlocks</th>
								<th title="Does repository use Rotated Drives" style="text-align:left">Rotated Drives</th>
								<th title="Does repository use Immutability?" style="text-align:left">Use Immutability</th>
								<th title="Repository Type" style="text-align:left">Type</th>
							</tr>
							<xsl:for-each select="repository">
								<tr>
									<td title="Name">
										<xsl:value-of select="Name"/>
									</td>
									<td title="SOBR">
										<xsl:value-of select="sobr"/>
									</td>

									<td title="Set Tasks" bgcolor="">
										<xsl:if test="Tasks/@color='under'">
											<xsl:attribute name="bgcolor">
												<xsl:text>GreenYellow</xsl:text>
											</xsl:attribute>
											<script>
											</script>
										</xsl:if>
										<xsl:if test="Tasks/@color='over'">
											<xsl:attribute name="bgcolor">
												<xsl:text>orangered</xsl:text>
											</xsl:attribute>
										</xsl:if>
										<xsl:value-of select="Tasks"/>
									</td>
									<td title="Cores" style="text-align:right">
										<xsl:value-of select="Cores"/>
									</td>
									<td title="RAM" style="text-align:right">
										<xsl:value-of select="RAM"/>
									</td>
									<td title="AutoGate" style="text-align:right">
										<xsl:value-of select="AutoGate"/>
									</td>
									<td title="Host">
										<xsl:value-of select="Host"/>
									</td>
									<td title="Path">
										<xsl:value-of select="Path"/>
									</td>
									<td title="Free Space" style="text-align:right">
										<xsl:value-of select="freespace"/>
									</td>
									<td title="Total Space" style="text-align:right">
										<xsl:value-of select="totalspace"/>
									</td>
									<!--<td title="Free Space %" style="text-align:right">
								<xsl:value-of select="freespacepercent"/>
							</td>-->
									<td title ="% Free Space" bgcolor ="" style="text-align:right">
										<xsl:if test="not(freespacepercent >20) and (freespacepercent > 0)">
											<xsl:attribute name="bgcolor">
												<xsl:text>orangered</xsl:text>
											</xsl:attribute>
										</xsl:if>

										<xsl:value-of select="freespacepercent"/>
									</td>
									<!--<td title="Per-VM" style="text-align:right">
								<xsl:value-of select="pervm"/>
							</td>-->
									<td title="UnCompress" style="text-align:right">
										<xsl:value-of select="uncompress"/>
									</td>
									<td title="Align Blocks" style="text-align:right">
										<xsl:value-of select="align"/>
									</td>
									<td title="Rotated Drives" style="text-align:right">
										<xsl:value-of select="rotate"/>
									</td>
									<td title="Immutable" style="text-align:right">
										<xsl:value-of select="immute"/>
									</td>
									<td title="Type">
										<xsl:value-of select="type"/>
									</td>


								</tr>
							</xsl:for-each>
						</table>
						* Red/Orange Highlights indicate an area for further investigation such as over-tasking or low available free space
						<br></br><br></br>
						<div class="hdr">Summary:</div><br></br>
						<div class="i2">
							•A SOBR is created by taking one, or many individual Veeam repositories (basic, or hardened) and placing them into the SOBR construct. The individual repositories comprising the SOBR are referred to as 'Extents'.
						</div>
						<div class="i2">
							•This section of the Health Check report details repository setting as they are configured in the Veeam Backup and Replication UI.  These settings can affect, or help performance of a backup job.
						</div><br></br>
						<div class="hdr">Notes:</div><br></br>
						<div class="subhdr">Set Tasks:</div>
						<div class="i2">
							•This column is the max concurrent task setting allowed to a repository. It specifies the maximum allowed number of concurrent tasks for the backup repository. If this value is exceeded, Veeam Backup &amp; Replication will not start a new task until one of current tasks finishes. For more information, see <a href="https://helpcenter.veeam.com/docs/backup/vsphere/limiting_tasks.html">Limiting the Number of Concurrent Tasks</a>.
						</div>
						<div class="i2">
							•To determine the number of tasks/cores needed for a backup repository(s) use the following <a href="https://bp.veeam.com/vbr/2_Design_Structures/D_Veeam_Components/D_backup_repositories/">formula</a>:
						</div>
						<div class="i3">oThe number of proxy vCPU/cores / 3 = the number of needed repository tasks.</div>
						<div class="i2">
							•The setting should align with the next two columns: <a style="font-weight: bold">Cores</a>, and <a style="font-weight:bold">RAM(GB)</a>
						</div>
						<div class="subhdr">Cores:</div>
						<div class="i2">•This number is equal to the number of CPU cores in the repository server.</div>
						<div class="subhdr">RAM:</div>
						<div class="i2">•This number is equal to the amount of RAM in the repository server.</div>
						<div class="i2">•The amount of RAM should be 4x the number of cores.</div>

						<div class="subhdr">Free Space (TB) and Free Space %</div>
						<div class="i2">•Best practice is to maintain 20% free space in a repository. This is especially important if the SOBR extents are formatted with XFS/REFS file systems as the block cloning and spaceless synthetic backups need to have all the relevant Veeam backup files on the same disk.  Free space is also important on other file systems as Veeam needs working space to create synthetic full backups.</div>
						<div class="i2">•In the General Options section of the VBR Console an alarm can be set to alert on repository low free disk space percentage.</div>
						<div class="subhdr">Align Blocks</div>
						<div class="i2">
							•For storage systems using a fixed block size, select the <a style="font-weight:bold">Align backup file data blocks</a> check box. Veeam Backup &amp; Replication will align VM data saved to a backup file at a 4 KB block boundary.
						</div>
						<div class="i2">•This setting also helps speed data transfer and uses less RAM when transferring data to a block-based repository</div>
						<div class="i2">
							•For block-based repositories in Veeam Backup and Replication v11 and newer this setting should be set to <a style="font-weight:bold">true</a>.
						</div>
						<h5>
							<a href="#top">Back To Top</a>
						</h5>
					</body>
				</html>
			</div>
		</div>
	</xsl:template>
	<xsl:template match="/root/repositories">
		<div id="linkrepo">
			<h2>
				<u>Standalone Repository Details</u>
			</h2>
			<button type="button" class="collapsible">Show Repository Details</button>
			<div class="content">
				<html>
					<body>

						<table border="1">
							<tr >
								<th title="Repository name" style="text-align:left">Name</th>
								<!--<th style="text-align:left">Tasks</th>-->
								<th title="Total tasks assigned in repository properties (-1 = unlimited)" style="text-align:left">Set Tasks</th>
								<th title="Total detected CPU cores on repository server" style="text-align:left">Cores</th>
								<th title="Total detected RAM on repository server" style="text-align:left">RAM(GB)</th>
								<th title="Total jobs assigned to the repository" style="text-align:left">JobCount</th>
								<th title="Does the repostory use Automatic Gateway?" style="text-align:left">AutoGateway</th>
								<th title="Host assigned as gateway if not using Automatic" style="text-align:left">Host</th>
								<th title="Path assigned to store the backup files" style="text-align:left">Path</th>
								<th title="Detected free space based on most recent cache data" style="text-align:left">FreeSpace(TB)</th>
								<th title="Detected total space." style="text-align:left">TotalSpace(TB)</th>
								<th title="Calculated free space percent remaining based on most recent cache data" style="text-align:left">FreeSpace %</th>
								<th title="Does the repository use per-VM backup files?" styel="text-aligh:left">Per-VM</th>
								<th title="Does the repository decompress backup files before writing to disk?" style="text-align:left">DeCompress</th>
								<th title="Is the repository configured to alight data blocks?" style="text-align:left">AlignBlocks</th>
								<th title="Is the repository backed by rotated drives?" style="text-align:left">Rotated Drives</th>
								<th title="Does the repsoitory use Immutability?" style="text-align:left">Use Immutability</th>
								<th title="Repository Type" style="text-align:left">Type</th>
							</tr>
							<xsl:for-each select="repository">
								<tr>
									<td title="Name">
										<xsl:value-of select="Name"/>
									</td>
									<td title="Tasks" bgcolor="">
										<xsl:if test="Tasks/@color='under'">
											<xsl:attribute name="bgcolor">
												<xsl:text>GreenYellow</xsl:text>

											</xsl:attribute>
											<xsl:attribute name="title">
												<xsl:text>Under Utilized</xsl:text>
											</xsl:attribute>
										</xsl:if>
										<xsl:if test="Tasks/@color='over'">
											<xsl:attribute name="bgcolor">
												<xsl:text>orangered</xsl:text>

											</xsl:attribute>
											<xsl:attribute name="title">
												<xsl:text>Over Utilized</xsl:text>
											</xsl:attribute>
										</xsl:if>
										<xsl:value-of select="Tasks"/>
									</td>
									<td title="CPU Core Count" style="text-align:right">
										<xsl:value-of select="Cores"/>
									</td>
									<td title="RAM" style="text-align:right">
										<xsl:value-of select="RAM"/>
									</td>
									<td title="Job Count" style="text-align:right">
										<xsl:value-of select="jobcount"/>
									</td>
									<td title="AutoGate" style="text-align:right">
										<xsl:value-of select="AutoGate"/>
									</td>
									<td title="Host">
										<xsl:value-of select="host"/>
									</td>
									<td title="Path">
										<xsl:value-of select="Path"/>
									</td>
									<td title="Free Space" style="text-align:right">
										<xsl:value-of select="freespace"/>
									</td>
									<td title="Total Space" style="text-align:right">
										<xsl:value-of select="totalspace"/>
									</td>
									<td title ="% Free Space" bgcolor ="" style="text-align:right">
										<xsl:if test="not(freespacepercent >20) and (freespacepercent > 0)">
											<xsl:attribute name="bgcolor">
												<xsl:text>orangered</xsl:text>
											</xsl:attribute>
										</xsl:if>

										<xsl:value-of select="freespacepercent"/>
									</td>
									<td title="Per-VM" style="text-align:right">
										<xsl:value-of select="pervm"/>
									</td>
									<td title="UnCompress" style="text-align:right">
										<xsl:value-of select="uncompress"/>
									</td>
									<td title="Align Blocks" style="text-align:right">
										<xsl:value-of select="align"/>
									</td>
									<td title="Rotated Drives" style="text-align:right">
										<xsl:value-of select="rotate"/>
									</td>
									<td title="Immuteable" style="text-align:right">
										<xsl:value-of select="immute"/>
									</td>
									<td title="Type">
										<xsl:value-of select="type"/>
									</td>


								</tr>
							</xsl:for-each>
						</table>
						* Red/Orange Highlights indicate an area for further investigation, such as over-tasking or low available free space

						<br></br><br></br>
						<div class="hdr">Summary:</div><br></br>
						<div class="i2">
							•Standalone Repositories includes those not configured as part of a SOBR
						</div>
						<div class="i2">
							•This section of the Health Check report details repository setting as they are configured in the Veeam Backup and Replication UI.  These settings can affect, or help performance of a backup job.
						</div><br></br>
						<div class="hdr">Notes:</div><br></br>
						<div class="subhdr">Set Tasks:</div>
						<div class="i2">
							•This column is the max concurrent task setting allowed to a repository. It specifies the maximum allowed number of concurrent tasks for the backup repository. If this value is exceeded, Veeam Backup &amp; Replication will not start a new task until one of current tasks finishes. For more information, see <a href="https://helpcenter.veeam.com/docs/backup/vsphere/limiting_tasks.html">Limiting the Number of Concurrent Tasks</a>.
						</div>
						<div class="i2">
							•To determine the number of tasks/cores needed for a backup repository(s) use the following <a href="https://bp.veeam.com/vbr/2_Design_Structures/D_Veeam_Components/D_backup_repositories/">formula</a>:
						</div>
						<div class="i3">oThe number of proxy vCPU/cores / 3 = the number of needed repository tasks.</div>
						<div class="i2">
							•The setting should align with the next two columns: <a style="font-weight: bold">Cores</a>, and <a style="font-weight:bold">RAM(GB)</a>
						</div>
						<div class="subhdr">Cores:</div>
						<div class="i2">•This number is equal to the number of CPU cores in the repository server.</div>
						<div class="subhdr">RAM:</div>
						<div class="i2">•This number is equal to the amount of RAM in the repository server.</div>
						<div class="i2">•The amount of RAM should be 4x the number of cores.</div>

						<div class="subhdr">Free Space (TB) and Free Space %</div>
						<div class="i2">•Best practice is to maintain 20% free space in a repository. This is especially important if the repositories are formatted with XFS/REFS file systems as the block cloning and spaceless synthetic backups need to have all the relevant Veeam backup files on the same disk.  Free space is also important on other file systems as Veeam needs space to create synthetic full backups.</div>
						<div class="i2">•In the General Options section of the VBR console an alarm can be set to alert on repository free disk space percentage.</div>
						<div class="subhdr">Align Blocks</div>
						<div class="i2">
							•For storage systems using a fixed block size, select the <a style="font-weight:bold">Align backup file data blocks</a> check box. Veeam Backup &amp; Replication will align VM data saved to a backup file at a 4 KB block boundary.
						</div>
						<div class="i2">•This setting also helps speed data transfer and uses less RAM when transferring data to a block-based repository</div>
						<div class="i2">
							•For block-based repositories in Veeam Backup and Replication v11 and newer this setting should be set to <a style="font-weight:bold">true</a>.
						</div>
						<h5>
							<a href="#top">Back To Top</a>
						</h5>
					</body>
				</html>
			</div>
		</div>
	</xsl:template>
	<xsl:key name="proxyHeader" match="/root/proxies/proxy/td/@headerName" use="." />
	<xsl:template match="/root/proxies" name="proxy">
		<div id="linkproxy">
			<h2>
				<u>Proxy Info</u>
			</h2>
			<button type="button" class="collapsible">Show Proxy Summary</button>
			<div class="content">
				<html>
					<body>
						<table border="1" >
							<xsl:for-each select="proxy/td/@headerName[generate-id() = generate-id(key('proxyHeader',.)[1])]">
								<!--<xsl:for-each select="distinct-values(/proxy/td/@headerName)">-->
								<th title="{../@tooltip}">
									<!--tooltip works-->
									<xsl:value-of select="."/>
								</th>
							</xsl:for-each>

							<xsl:for-each select="proxy">
								<tr>
									<xsl:for-each select ="td">
										<td>
											<xsl:value-of select="current()"/>
										</td>
									</xsl:for-each>

								</tr>

							</xsl:for-each>

						</table><br></br>
						<div class="hdr">Role Summary:</div>
						Veeam proxies are a logical datamover component. There are two types of proxies: (<a href="https://helpcenter.veeam.com/docs/backup/vsphere/backup_proxy.html?zoom_highlight=proxy&amp;ver=110">Backup Proxies</a>, <a href="https://helpcenter.veeam.com/docs/backup/vsphere/cdp_proxy.html?ver=110">CDP Proxies</a>). Backup Proxies are further subdivided based on function or platform e.g. File Proxies for NAS-based backups, or different source hypervisors.
						<div class="i2">•	Backup proxies sit between source data (VMs or File Shares) and the backup repositories. Their role is to process backup jobs and deliver backup traffic to the repositories.</div>
						<div class="i3">
							o	VM Backup proxies can leverage different <a href="https://helpcenter.veeam.com/docs/backup/vsphere/transport_modes.html?ver=110">transport modes</a>.
						</div>
						<div class="i3">
							o	File Backup proxies can backup source data from manual or automated snapshot paths for <a href="https://helpcenter.veeam.com/docs/backup/vsphere/file_share_backup_nfs_share_advanced_settings.html?ver=110"> NFS</a>, <a href="https://helpcenter.veeam.com/docs/backup/vsphere/file_share_backup_smb_share_advanced_settings.html?ver=110">SMB</a>, Enterprise NAS filers. (Note: Managed servers do not leverage File Backup Proxies)
						</div>
						<div class="i3">o	Backup Proxies do not store or cache any data locally.</div>
						<div class="i2">•	CDP proxies process CDP policies and operate as data movers between source and target VMware hosts.</div>
						<div class="i3">
							o	CDP proxies require a <a href="https://helpcenter.veeam.com/docs/backup/vsphere/system_requirements.html?ver=110#vmware-cdp-proxy-server">local cache.</a>
						</div>
						<br></br>
						<div class="hdr">Notes:</div>
						<div class="i2">
							•	Review the “host” column and identify the host(s) that support <a href="https://helpcenter.veeam.com/docs/backup/vsphere/limiting_tasks.html?ver=110#task-limitation-for-components-with-several-roles">multiple</a> proxy roles:
						</div>
						<div class="i3">
							o	Roles can be combined providing you allocate <a href="https://helpcenter.veeam.com/docs/backup/vsphere/limiting_tasks.html?ver=110">enough resources</a>.
						</div>
						<div class="i4">	If roles are non-concurrent then allocate the max resources calculated across all supported roles.</div>
						<div class="i4">
								If roles are concurrent then allocate <a href="https://helpcenter.veeam.com/docs/backup/vsphere/limiting_tasks.html?ver=110#task-limitation-for-components-with-several-roles">enough</a> resources for all roles
						</div>
						<div class="i4">	Keep in mind that CDP proxies are constantly active and transferring data, therefore it is imperative to “reserve” enough resources for its role and add what is necessary the combined role(s).</div>
						<div class="i2">•	CDP proxies:</div>
						<div class="i3">
							o	Ensure <a href="https://helpcenter.veeam.com/docs/backup/vsphere/cdp_proxy.html?zoom_highlight=CDP+cache&amp;ver=110#vmware-cdp-proxy-cache">cache</a> is properly sized
						</div>
						<div class="i3">o	Flag CDP proxies with cache located on the C:\ drive as a potential risk.</div>
						<div class="i2">
							•	Compare assigned <a href="https://helpcenter.veeam.com/docs/backup/vsphere/limiting_tasks.html?ver=110">tasks</a> and core count and identify <a href="https://helpcenter.veeam.com/docs/backup/vsphere/limiting_tasks.html?ver=110#task-limitation-for-backup-proxies">oversubscription</a>.
						</div>
						<div class="i2">
							•	Confirm the RAM to core ratio is <a href="https://helpcenter.veeam.com/docs/backup/vsphere/system_requirements.html?ver=110#vmware-cdp-proxy-server">adequate</a>.
						</div>
						<div class="i2">
							•	Ensure there are enough resources for the base OS. (<a href="https://helpcenter.veeam.com/docs/backup/vsphere/system_requirements.html?ver=110#backup-proxy-server">backup proxy</a>, <a href="https://helpcenter.veeam.com/docs/backup/vsphere/system_requirements.html?ver=110#vmware-cdp-proxy-server">CDP proxy</a>)
						</div>
						<div class="i2">
							•	Check selected <a href="https://helpcenter.veeam.com/docs/backup/vsphere/transport_modes.html?zoom_highlight=transport+mode&amp;ver=110">transport mode</a> and highlight where <a href="https://helpcenter.veeam.com/docs/backup/vsphere/network_mode_failover.html?ver=110">failover to network mode</a> is disabled when jobs are failing or where failover to network is enabled for jobs to run slower than expected (NBD traffic could flow through the <a href="https://helpcenter.veeam.com/docs/backup/vsphere/select_backup_network.html?zoom_highlight=preferred+network&amp;ver=110">wrong network</a>).
						</div>
						<div class="i2">
							•	Hyper-V off host proxies should <a href="https://helpcenter.veeam.com/docs/backup/hyperv/offhost_backup_proxy.html?ver=110">match</a> protected Hyper-V host versions.
						</div>




						<h5>
							<a href="#top">Back To Top</a>
						</h5>
					</body>
				</html>
			</div>
		</div>

	</xsl:template>

	<!--<xsl:key name="header" match="/root/category/entity/td/@headerName" use="." />
	<xsl:template match="/root/TEMPLATEfromProxies" name="TEMPLATE">
		<div id="linkproxy">
			<h2>
				<u>Proxy Info</u>
			</h2>
			<button type="button" class="collapsible">Show Proxy Summary</button>
			<div class="content">
				<html>
					<body>
						<table border="1" >
							<xsl:for-each select="entity/td/@headerName[generate-id() = generate-id(key('proxyHeader',.)[1])]">
								-->
	<!--<xsl:for-each select="distinct-values(/proxy/td/@headerName)">-->
	<!--
								<th title="{../@tooltip}">
									-->
	<!--tooltip works-->
	<!--
									<xsl:value-of select="."/>
								</th>
							</xsl:for-each>

							<xsl:for-each select="proxy">
								<tr>
									<xsl:for-each select ="td">
										<td>
											<xsl:value-of select="current()"/>
										</td>
									</xsl:for-each>

								</tr>

							</xsl:for-each>

						</table><br></br>
						<div class="hdr">Role Summary:</div>
						Veeam proxies are a logical datamover component. There are two types of proxies: (<a href="https://helpcenter.veeam.com/docs/backup/vsphere/backup_proxy.html?zoom_highlight=proxy&amp;ver=110">Backup Proxies</a>, <a href="https://helpcenter.veeam.com/docs/backup/vsphere/cdp_proxy.html?ver=110">CDP Proxies</a>). Backup Proxies are further subdivided based on function or platform e.g. File Proxies for NAS-based backups, or different source hypervisors.
						<div class="i2">•	Backup proxies sit between source data (VMs or File Shares) and the backup repositories. Their role is to process backup jobs and deliver backup traffic to the repositories.</div>
						<div class="i3">
							o	VM Backup proxies can leverage different <a href="https://helpcenter.veeam.com/docs/backup/vsphere/transport_modes.html?ver=110">transport modes</a>.
						</div>

						<h5>
							<a href="#top">Back To Top</a>
						</h5>
					</body>
				</html>
			</div>
		</div>

	</xsl:template>-->



	<xsl:template match="/root/servers">

		<div id="linkserverList">
			<h2>
				<u>Managed Server Info</u>
			</h2>
			<button type="button" class="collapsible">Show Managed Server Details</button>
			<div class="content">
				<html>
					<body>
						<table border="1">
							<tr >
								<th title="Server or Share name">Name</th>
								<th title="Detected CPU Cores">Cores</th>
								<th title="Detected RAM">RAM</th>
								<th title="Server or Share Type">Type</th>
								<th title="VMware API Version">Api Version</th>
								<th title="Total VMs detected on this host with associated backups">Protected VMs</th>
								<th title="Total VMs detected on this host without associated backups">Not Protected VMs</th>
								<th title="Total VMs detected on this host">Total VMs</th>
								<th title="Is the Server a Veeam Proxy?">Is Proxy</th>
								<th title="Is the Server a Veeam Repository?">Is Repo</th>
								<th title="Is the Server a Veeam WAN Accelerator?">Is WAN Acc.</th>
								<th title="Is the Server unavailable?">Is UnAvailable</th>
							</tr>
							<xsl:for-each select="server">
								<tr>
									<td title="Name">
										<xsl:value-of select="Name"/>
									</td>
									<td title="Cores" style="text-align:right">
										<xsl:value-of select="Cores"/>
									</td>
									<td title="RAM" style="text-align:right">
										<xsl:value-of select="RAM"/>
									</td>
									<td title="Type">
										<xsl:value-of select="Type"/>
									</td>
									<td title="API Version" style="text-align:right">
										<xsl:value-of select="ApiVersion"/>
									</td>
									<td title="" style="text-align:right">
										<xsl:value-of select="protectedVms"/>
									</td>
									<td title="" style="text-align:right">
										<xsl:value-of select="unProtectedVms"/>
									</td>
									<td title="" style="text-align:right">
										<xsl:value-of select="totalVms"/>
									</td>
									<td  title="Is Proxy?" style="text-align:right">
										<xsl:value-of select="proxyrole"/>
									</td>
									<td title="Is Repo?" style="text-align:right">
										<xsl:value-of select="repo"/>
									</td>
									<td title="Is WAN Acc?" style="text-align:right">
										<xsl:value-of select="wanacc"/>
									</td>
									<td title="Is Unavailable?" style="text-align:right">
										<xsl:value-of select="isavailable"/>
									</td>
								</tr>
							</xsl:for-each>
						</table>
						<br></br>
						<div class="hdr">Summary:</div>
						This lists all servers <a href="https://helpcenter.veeam.com/docs/backup/vsphere/setup_add_server.html?ver=110">managed</a> by Veeam that identify data sources (Hypervisors),  backup infrastructure servers (windows, linux) and Veeam Backup for AWS/Azure/GCP appliances.
						<br></br><br></br>
						<div class="hdr">Notes:</div>
						<div class="i2">
							•	Review the API version column and ensure they are <a href="https://helpcenter.veeam.com/docs/backup/vsphere/platform_support.html?ver=110">supported</a> and meet the minimum <a href="https://helpcenter.veeam.com/docs/backup/vsphere/system_requirements.html?ver=110">requirements</a>.
						</div>
						<div class="i2">
							•	Highlight host supporting multiple roles and confirm <a href="">enough resources</a> are available to support them.
						</div>

						<h5>
							<a href="#top">Back To Top</a>
						</h5>
					</body>
				</html>
			</div>
		</div>
	</xsl:template>
	<xsl:template match="/root/jobSummary">
		<div id ="linkjobSummary">
			<h2>
				<u>Job Summary</u>
			</h2>
		</div>

		<html>
			<body>

				<!--Summary of servers manually added into Veeam<br></br>-->
				<xsl:value-of select="info"/>
				<table border="1" title="JobSummary">
					<tr >
						<th title="">Type</th>
						<th title="" style="text-align:left">Total</th>
					</tr>
					<xsl:for-each select="summary">
						<tr>
							<td title="" style="text-align:right">
								<xsl:value-of select="type"/>
							</td>
							<td title="" style="text-align:right">
								<xsl:value-of select="typeCount"/>
							</td>
						</tr>
					</xsl:for-each>
				</table>
				<button type="button" class="collapsible">Show Job Summary Notes</button>
				<div class="content">
					<br></br>

					<br></br>
					<div class="hdr">Summary:</div><br></br>
					Counts for all job types created on the Veeam Backup and replication server, and sum total of all jobs. The number of concurrently running jobs directly impacts the sizing and performance of the VBR server.
					<br></br><br></br>
					<div class="hdr">Notes:</div>
					<br></br>
					Be aware that some jobs, such as Backup Copy Jobs, are often configured to run continuously.<br></br>
					See <a href="#jobConcurrency7">Concurrency Table</a>

					<br></br>
					<h5>
						<a href="#top">Back To Top</a>
					</h5>
				</div>
			</body>
		</html>
	</xsl:template>
	<xsl:template match="/root/concurrencyChart_job7">
		<div id="linkjobConcurrency7">
			<h2>
				<u>Job Concurrency Table (7 days)</u>
			</h2>
			<button type="button" class="collapsible">Show Concurrency Table</button>
			<div class="content">
				<html>
					<body>
						<table border="1">
							<tr >
								<th title="">Hour</th>
								<th title="">Sunday</th>
								<th title="">Monday</th>
								<th title="">Tuesday</th>
								<th title="">Wednesday</th>
								<th title="">Thursday</th>
								<th title="">Friday</th>
								<th title="">Saturday</th>
							</tr>
							<xsl:for-each select="day">
								<tr>
									<td title="" style="text-align:right">
										<xsl:value-of select="hour"/>
									</td>
									<td title="">
										<xsl:value-of select="count[@day='Sunday']"/>
									</td>
									<td title="">
										<xsl:value-of select="count[@day='Monday']"/>
									</td>
									<td title="">
										<xsl:value-of select="count[@day='Tuesday']"/>
									</td>
									<td title="">
										<xsl:value-of select="count[@day='Wednesday']"/>
									</td>
									<td title="">
										<xsl:value-of select="count[@day='Thursday']"/>
									</td>
									<td title="">
										<xsl:value-of select="count[@day='Friday']"/>
									</td>
									<td title="">
										<xsl:value-of select="count[@day='Saturday']"/>
									</td>




								</tr>
							</xsl:for-each>
						</table>
						<br></br>
						<div class="hdr">Summary:</div><br></br>
						The concurrency table is meant to serve as a heat map. Each hour of each day is represented. The number in each cell represents the highest calculated number of total concurrent running jobs within the specific hour. This data can be used to ensure more accurate VBR server sizing.
						<br></br><br></br>
						<div class="hdr">Notes:</div><br></br>
						<div class="subhdr">Compute</div><br></br>
						<div class="i2">Recommended Veeam backup server configuration is 1 CPU core (physical or virtual) and 4 GB RAM per 10 concurrently running jobs. Concurrent jobs include any running backup or replication jobs as well as any job with a continuous schedule such as backup copy jobs and tape jobs.</div>
						<br></br>
						<div class="i2">
							<a href="https://bp.veeam.com/vbr/3_Build_structures/B_Veeam_Components/B_VBR_Server/Backup_Server.html" target="_blank">
								The minimum recommendation is 2 CPU cores and 8 GB RAM.
							</a>
						</div><br>
						</br>
						<div class ="i2">
							If the required resources exceed the current VBR CPU and RAM, job scheduling should be analyzed to minimize the number of concurrent jobs. If job scheduling changes are not possible, multiple VBR servers should be deployed, and jobs migrated to the new servers.
						</div>
						<br>
						</br>
						<div class="subhdr">Veeam Microsoft SQL Database</div><br></br>
						<!--<div class="i2">
					Veeam Backup &amp; Replication may consume high amounts of CPU and RAM while processing backup or replication jobs. To achieve better performance and load balancing it is necessary to provide sufficient RAM and CPU resources to Veeam components. Remember to add additional resources, if the backup server is responsible for multiple roles, such as repository server or backup proxy.
				</div><br></br>-->
						<div class="i2">
							<div class="bld">
								Please follow these guidelines:
							</div>
						</div><br></br>
						<div class="i4">
							<table border="1">
								<tr bgcolor="lightblue">
									<th>Number of concurrently running jobs</th>
									<th>CPU</th>
									<th>RAM</th>
								</tr>
								<tr>
									<td>Up to 25</td>
									<td>2 CPU</td>
									<td>4 GB</td>
								</tr>
								<tr>
									<td>Up to 50</td>
									<td>4 CPU</td>
									<td>8 GB</td>
								</tr>
								<tr>
									<td>Up to 100</td>
									<td>8 CPU</td>
									<td>16 GB</td>
								</tr>
							</table>
						</div>
						<br></br>
						<div class="i2">
							It is recommended to install SQL Standard or Enterprise Edition if any of the following apply:
						</div><br></br>
						<div class="i3">• When protecting more than 500 servers. The max database size allowed by Express Edition is usually sufficient, so do not consider this a constraint. Veeam Backup &amp; Replication console and job processing may however slow down as a result of CPU and RAM constraints on the SQL Server Express instance.</div>
						<div class="i3">• When using Files to Tape jobs extensively, the database may grow significantly, and the 10 GB limitation may be exceeded quickly.</div>
						<div class="i3">
							• When unable to configure an external staging server for use with Veeam Explorer for Microsoft SQL Server or Veeam Explorer for Microsoft SharePoint. When working with databases larger than 10 GB, SQL Server Express cannot mount the databases.<br></br>
						</div>
						<div class="i3">
							• When databases are using advanced features of Microsoft SQL Server. Such as encryption or table partitioning, the licensing level of the staging server (local or remote) must match the level of the original instance.
						</div>
						<h5>
							<a href="#top">Back To Top</a>
						</h5>
					</body>
				</html>
			</div>
		</div>
	</xsl:template>
	<xsl:template match="/root/concurrencyChart_task7">
		<div id="linktaskConcurrency7">
			<h2>
				<u>VM Task Concurrency Table (7 days)</u>
			</h2>
			<button type="button" class="collapsible">Show Concurrency Table</button>
			<div class="content">
				<html>
					<body>
						<table border="1">
							<tr >
								<th title="">Hour</th>
								<th title="">Sunday</th>
								<th title="">Monday</th>
								<th title="">Tuesday</th>
								<th title="">Wednesday</th>
								<th title="">Thursday</th>
								<th title="">Friday</th>
								<th title="">Saturday</th>
							</tr>
							<xsl:for-each select="day">
								<tr>
									<td title="" style="text-align:right">
										<xsl:value-of select="hour"/>
									</td>
									<td title="">
										<xsl:value-of select="count[@day='Sunday']"/>
									</td>
									<td title="">
										<xsl:value-of select="count[@day='Monday']"/>
									</td>
									<td title="">
										<xsl:value-of select="count[@day='Tuesday']"/>
									</td>
									<td title="">
										<xsl:value-of select="count[@day='Wednesday']"/>
									</td>
									<td title="">
										<xsl:value-of select="count[@day='Thursday']"/>
									</td>
									<td title="">
										<xsl:value-of select="count[@day='Friday']"/>
									</td>
									<td title="">
										<xsl:value-of select="count[@day='Saturday']"/>
									</td>




								</tr>
							</xsl:for-each>
						</table>
						<br></br>
						<div class="hdr">Summary:</div><br></br>
						The concurrency table is meant to serve as a heat map. Each hour of each day is represented. The number in each cell represents the highest calculated number of concurrent running Tasks within the specific hour. This data can be used to aid in job scheduling and proxy/repository task sizing.
						<br></br><br></br>
						<div class="hdr">Notes:</div><br></br>
						<div class="i2">
							Use this chart to check maximum concurrency. Each task here should be backed by 1 corresponding Proxy Task and 1/3 corresponding Repository Task.
						</div>
						<h5>
							<a href="#top">Back To Top</a>
						</h5>
					</body>
				</html>
			</div>
		</div>
	</xsl:template>
	<xsl:template match="/root/regOptions">
		<div id="linkregkeys">
			<h2>
				<u>Non-Default Registry Keys</u>
			</h2>
			<button type="button" class="collapsible">Show RegKey Details</button>
			<div class="content">
				<html>
					<body>
						<table border="1">
							<tr >
								<th title="">Key</th>
								<th title="">Value</th>
							</tr>
							<xsl:for-each select="rOpt">
								<tr>
									<td title="" style="text-align:right">
										<xsl:value-of select="key"/>
									</td>
									<td title="">
										<xsl:value-of select="value"/>
									</td>
								</tr>
							</xsl:for-each>
						</table>
						<br></br>
						<div class="hdr">Summary:</div><br></br>
						This table details any Registry Keys that are different from the default keys set during a basic install of Veeam Backup &amp; Replication. Specifically in <a class="bld">HKLM\SOFTWARE\Veeam\Veeam Backup and Replication</a>
						<br></br>
						<div class="hdr">Notes:</div><br></br>
						Because some registry keys are tied to bug fixes and/or other tweaks recommended for various reasons, it's best advised to consult your past support case emails and notes or contact your Veeam Engineer for any questions regarding keys shown here.<br></br>
						<br></br>
						Some keys, such as LoggingLevel and other "Log" related keys are perfectly fine to adjust, if required, when following the corresponding <a href="https://www.veeam.com/kb1825">KB Article.</a>
						<br></br><br></br>
						<!--<div class="hdr">Identify Potential Issues:</div><br></br>-->

						<h5>
							<a href="#top">Back To Top</a>
						</h5>
					</body>
				</html>
			</div>
		</div>
	</xsl:template>
	<xsl:template match="/root/jobs">
		<div id="linkjobs">
			<h2>
				<u>Job Info</u>
			</h2>
			<button type="button" class="collapsible">Show Job Details</button>
			<div class="content">
				<html>
					<body>
						<h3>
							Job Info<h5>
								<!--Summary of servers manually added into Veeam<br></br>-->
								<xsl:value-of select="info"/>
							</h5>
						</h3>
						<div class="hdr">Summary:</div>
						<div class="i2">•	Jobs define source data, destination, schedule, and advanced settings on handling that source data. There are multiple job types within Veeam Backup &amp; Replication, including Backup, Backup Copy, CDP Policy, NAS Backup, and more. Not all job types will have data in every column.</div>
						<br></br>
						<div class="hdr">Notes:</div>
						<div class="i2">
							•	Verify that the Restore Points and Schedule Options listed for the Job match with the source application’s required RPO. See <a href="https://helpcenter.veeam.com/docs/backup/vsphere/backup_job_schedule_vm.html?ver=110">Job Scheduling</a>.
						</div>
						<table border="1">
							<tr >
								<th title="Job Name">Name</th>
								<th title="Target Repository Name">Repository</th>
								<th title="Actual used size of source data in job">Source Size</th>
								<th title="Desired Restore Points to be kept">Restore Points</th>
								<th title="Is encryption enabled for the job?">Encrypted</th>
								<th title="">Job Type</th>
								<th title="">Algorithm</th>
								<th title="">Schedule Enabled Time</th>
								<th title="">Full Backup Days</th>
								<th title="">Full Backup Schedule</th>
								<th title="">Schedule Options</th>
								<th title="">Transform Full To Synth</th>
								<th title="">Transform Inc To Synth</th>
								<th title="">Transform Days</th>
							</tr>
							<xsl:for-each select="job">
								<tr>
									<td title="Name" style="text-align:right">
										<xsl:value-of select="name"/>
									</td>
									<td title="Repository">
										<xsl:value-of select="repo"/>
									</td>
									<td title="Actual size of source data">
										<xsl:value-of select ="sourceSize"/>
									</td>
									<td title="Restore Points" style="text-align:right">
										<xsl:value-of select="restorePoints"/>
									</td>
									<td title="">
										<xsl:value-of select ="encrypted"/>
									</td>
									<td title="Job Type">
										<xsl:value-of select="jobType"/>
									</td>
									<td title="">

										<xsl:value-of select="alg"/>
									</td>
									<td title="" style="text-align:right">
										<xsl:value-of select="scheduleEnabledTime"/>
									</td>
									<td title="">
										<xsl:value-of select="fulldays"/>
									</td>
									<td title="">
										<xsl:value-of select="fullkind"/>
									</td>


									<td title="Schedule Options">
										<xsl:value-of select="scheduleoptions"/>
									</td>

									<td title="Transform Full to Synthetic" style="text-align:right">
										<xsl:value-of select="transformfulltosynth"/>
									</td>
									<td title="Transform Increment to Synthetic" style="text-align:right">
										<xsl:value-of select="transforminctosynth"/>
									</td>
									<td title="Transform days" style="text-align:right">
										<xsl:value-of select="transformdays"/>
									</td>
								</tr>
							</xsl:for-each>
						</table>
						<br></br>


						<h5>
							<a href="#top">Back To Top</a>
						</h5>
					</body>
				</html>
			</div>
		</div>
	</xsl:template>

	<!--<xsl:template match="/root/license">
	
  </xsl:template>-->
	<xsl:template match="/root/jobSessionsSummary">

		<div id ="linkt">
			<h2>
				<u>Job Session Summary (7 Days)</u>
			</h2>
			<button type="button" class="collapsible">Show Job Details</button>
			<div class="content">
				<html>
					<header>
						<link rel="stylesheet" type="text/css" href="vhc.css"></link>
					</header>
					<body>
						<table border="1">
							<tr >
								<th title="Job Name">
									Job Name
								</th>
								<th title="Number of Items in the job">
									Items
								</th>
								<th title="Minimum calculated run time in minutes">
									Min Time (min)
								</th>
								<th title="Maximum calculated run time in minutes">
									Max Time (min)
								</th>
								<th title="Average run time in minutes">
									Avg Time (min)
								</th>
								<th title="Total detected sessions">
									Total Sessions
								</th>
								<th title="Success rate over all detected sessions">
									Success Rate %
								</th>
								<th title="Average calculated size of the backup files">
									Avg Backup Size TB
								</th>
								<th title="Largest single backup file size for this job">
									Max Backup Size TB
								</th>
								<th title="Average calculated size of data to be backed up (provisioned, not actual)">
									Avg Data Size TB
								</th>
								<th title="Largest VM/Server size (provisioned, not actual)">
									Max Data Size TB
								</th>
								<th title="Average calculated changerate of the job">
									Avg ChangeRate %
								</th>
								<th title="Number of times the job had to wait for resources">
									Wait For Res. Count
								</th>
								<th title="Logest detected wait time the job experienced">
									Max Wait (dd.hh:mm:ss)
								</th>
								<th title="Average calculated wait time over all detected waits">
									Avg Wait (dd.hh:mm:ss)
								</th>
								<th title="Job Type">JobType</th>
							</tr>
							<xsl:for-each select="session">
								<tr>
									<td title="Job Name">
										<xsl:value-of select="name"/>
									</td>
									<td title="Items" style="text-align:right">
										<xsl:value-of select="items"/>
									</td>
									<td title="Min Time" style="text-align:right">
										<xsl:value-of select="mintime"/>
									</td>
									<td title="Max Time" style="text-align:right">
										<xsl:value-of select="maxtime"/>
									</td>
									<td title="Avg Time" style="text-align:right">
										<xsl:value-of select="avgtime"/>
									</td>
									<td title="Session count" style="text-align:right">
										<xsl:value-of select="sessions"/>
									</td>
									<td title="Success rate" style="text-align:right">
										<xsl:value-of select="successrate"/>
									</td>
									<td title="Avg backup size" style="text-align:right">
										<xsl:value-of select="avgBackupSize"/>
									</td>
									<td title="Max backup size" style="text-align:right">
										<xsl:value-of select="maxBackupSize"/>
									</td>
									<td title="Avg data size" style="text-align:right">
										<xsl:value-of select="avgDataSize"/>
									</td>
									<td title="Max data size" style="text-align:right">
										<xsl:value-of select="maxDataSize"/>
									</td>
									<td title="Change rate" style="text-align:right">
										<xsl:value-of select="changerate"/>
									</td>
									<td title="Wait count" style="text-align:right">
										<xsl:value-of select="waitCount"/>
									</td>
									<td title="Max wait" style="text-align:right">
										<xsl:value-of select="maxWait"/>
									</td>
									<td title="Avg wait" style="text-align:right">
										<xsl:value-of select="avgWait"/>
									</td>
									<td title="Type">
										<xsl:value-of select="type"/>
									</td>
								</tr>
							</xsl:for-each>
						</table>
						<br>
							<br></br>
						</br>
						<div class="hdr">Summary:</div>
						This table is meant to detail the recent history of individual jobs over the last 7 days.
						<div class="hdr">
							Notes:
							<br></br>
							<br></br>
						</div>
						<div class="subhdr">
							Waiting for Resources / job session length issues:
						</div>
						<div class="i2">
							•	Scheduling jobs to start at different time slots will help to distribute resources and prevent the bottleneck that is a job waiting on available resources. (Ex. Instead of scheduling your jobs to all start at 8:00PM, start one job at 8:00, another at 8:30, and another at 9:00.)<br></br>
						</div>
						<div class="i2">
							•	If resources allow, increase the number of concurrent tasks allowed on your proxies.
							<a href="https://helpcenter.veeam.com/docs/backup/vsphere/vmware_proxy_server.html?ver=110#:~:text=In%20the%20Max%20concurrent%20tasks%20field%2C%20specify%20the%20number%20of%20tasks%20that%20the%20backup%20proxy%20must%20handle%20in%20parallel.%20If%20this%20value%20is%20exceeded%2C%20the%20backup%20proxy%20will%20not%20start%20a%20new%20task%20until%20one%20of%20current%20tasks%20finishes.">
								See How to set max concurrent tasks.
							</a>
						</div>
						<div class="i3">o	If your backup proxy does not have adequate resources to increase tasks, and it is a virtual machine, you should consider increasing the amount of CPU and RAM available to the proxy.</div>
						<div class="i3">o	If adding resources to existing proxies is not an options, consider deploying additional backup proxies from within “Backup Infrastructure->Backup Proxies”</div>
						<div class="i2">•	Make sure that your backup job or replication job is selecting the correct proxies by viewing the job session statistics in the VBR Console.</div>
						<div class="i2">•	Investigate backup job performance. If specific jobs are taking longer to process than normal, check for warnings, compare the bottleneck statistics to previous jobs sessions, and try to isolate the problem to a specific proxy, repository, host, or datastore.</div>
						<div class="i3">o	Move larger VMs / servers to their own job and schedule to ensure conflict does not occur with faster completing job's backup window (schedule these jobs before all other jobs or after all other jobs, for example)</div>
						<div class="i2">•	Separate NAS Proxies, Cache Repos, and Repositories from VM proxies and VM and Agent Repositories</div>
						<div class="i2">
							•	<a href="https://helpcenter.veeam.com/docs/backup/vsphere/gateway_server.html?ver=110#gateway-servers-deployment">Use Static Gateways and Mount Servers</a>
							if possible to offload resources consumption required for synthetic operations, SOBR offload processing, backup copy jobs, and other tasks.
						</div>
						<div class="i2">
							•	If appropriate,<a href="https://www.veeam.com/kb2660">review Architecture Guidelines for deduplicating storage systems.</a>
						</div>
						<h5>
							<a href="#top">Back To Top</a>
						</h5>
					</body>
				</html>
			</div>
		</div>
	</xsl:template>


	<xsl:template match="/root/jobSessions">
		<div id="sessions"></div>
		<html>
			<body>
				<h3>
					Job Session Info<h5>
						<!--Summary of servers manually added into Veeam<br></br>-->
						<xsl:value-of select="info"/>
					</h5>
				</h3>

				<table border="1">
					<tr>
						<th title="Name of job">Job Name</th>
						<th title="Name of VM/Server within the job">VM Name</th>
						<th title="Job Algorithm">Alg</th>
						<th title="Primary detected bottleneck">Primary Bottleneck</th>
						<th title="Detected bottleneck breakdown">BottleNeck</th>
						<th title="Calculated compression ratio">CompressionRatio</th>
						<th title="Start time of the backup job">Start Time</th>
						<th title="Detected size of backup file">BackupSize</th>
						<th title="Detected size of original VM/server (provisioned, not actual)">DataSize</th>
						<th title="Calculated deduplication ratio">DedupRatio</th>
						<th title="Is this a retry run?">Is Retry</th>
						<th title="Duration of job in minutes">Job Duration</th>
						<th title="Shorted detected job duration in minutes">Min Time</th>
						<th title="Longest detected job duration in minutes">Max Time</th>
						<th title="Average job duration in minutes">Avg Time</th>
						<th title="Processing mode used in the job (blank = SAN)">Processing Mode</th>
						<th title="Final status of the job">Status</th>
						<th title="Duration of the VM/server within the job in minutes">Task Duration</th>
					</tr>
					<xsl:for-each select="session">
						<tr>
							<td title="Job Name">
								<xsl:value-of select="jobName"/>
							</td>
							<td title="VM Name" style="text-align:right">
								<xsl:value-of select="vmName"/>
							</td>
							<td title="Algorithm" style="text-align:right">
								<xsl:value-of select="alg"/>
							</td>
							<td title="Primary Bottleneck" style="text-align:right">
								<xsl:value-of select="primBottleneck"/>
							</td>
							<td title="Bottleneck" style="text-align:right">
								<xsl:value-of select="bottleneck"/>
							</td>
							<td title="Compression Ratio" style="text-align:right">
								<xsl:value-of select="compression"/>
							</td>
							<td title="Creation Time" style="text-align:right">
								<xsl:value-of select="creationtime"/>
							</td>
							<td title="Backup Size" style="text-align:right">
								<xsl:value-of select="backupsize"/>
							</td>
							<td title="Data Size" style="text-align:right">
								<xsl:value-of select="datasize"/>
							</td>
							<td title="Dedup Ratio" style="text-align:right">
								<xsl:value-of select="dedupratio"/>
							</td>
							<td title="Is Retry" style="text-align:right">
								<xsl:value-of select="isretry"/>
							</td>
							<td title="Job Duration" style="text-align:right">
								<xsl:value-of select="jobDuration"/>
							</td>
							<td title="Minimum Job Time" style="text-align:right">
								<xsl:value-of select="minTime"/>
							</td>
							<td title="Maximum Job Time" style="text-align:right">
								<xsl:value-of select="maxTime"/>
							</td>
							<td title="Average Job Time" style="text-align:right">
								<xsl:value-of select="avgTime"/>
							</td>

							<td title="Processing Mode" style="text-align:right">
								<xsl:value-of select="processingmode"/>
							</td>
							<td title="Status" style="text-align:right">
								<xsl:value-of select="status"/>
							</td>
							<td title="Task Duration" style="text-align:right">
								<xsl:value-of select="taskDuration"/>
							</td>


						</tr>
					</xsl:for-each>
				</table>
				<br></br>
				<div class="hdr">Summary</div>
				<div class="i2">
					•	This section contains information from the logs for defined jobs, including job time, duration, and bottlenecks.  With any backup, one part of the data transfer stream will always be the slowest, and thus listed as the <a href="https://helpcenter.veeam.com/docs/backup/vsphere/detecting_bottlenecks.html?ver=110">bottleneck</a>.  This does not imply that there is an issue, but that portion of the data path was just the slowest overall.
				</div>
				<br></br>
				<div class="hdr">Identify Potential Issues</div>
				<div class="i2">•	Job sessions with a Status of Failed should be investigated.  The failed job may be followed by the same job with an “Is Retry” value of True, which may have a Status of successful.</div>
				<div class="i2">•	Verify that the Primary Bottleneck is expected.  If most jobs are constantly reporting the source as the bottleneck, having another instance of that job list the proxy or destination as the Primary Bottleneck may indicate a change in the data path, or an overtaxed resource during that time frame.</div>
				<div class="i2">
					•	Double check that the <a href="https://helpcenter.veeam.com/docs/backup/vsphere/transport_modes.html?ver=110">Processing Mode</a> matches the desired method of transporting data defined in the job and proxies.  Having virtual proxies, but seeing a mode of “NBD” would indicate an issue for example.
				</div>
				<div class="i2">•	Last, check the Job Duration column for longer than normal job run times.</div>
				<h5>
					<a href="#top">Back To Top</a>
				</h5>
			</body>
		</html>
	</xsl:template>
	<xsl:template name="user.footer.content" match="footer" >
		<h2>Test Footer</h2>
	</xsl:template>


	<!--m365 area-->

	<xsl:key name="m365globalheader" match="/root/Global/td/@headerName" use="." />
	<xsl:template match="/root/Global" name="global">
		<div id="linkGlobal">
			<h2>
				<u>Global Info</u>
			</h2>
			<button type="button" class="collapsible">Show Global Information</button>
			<div class="content">
				<html>
					<body>
						<table border="1" >
							<xsl:for-each select="td/@headerName">
								<!--<xsl:for-each select="distinct-values(/proxy/td/@headerName)">-->
								<th title="{../@tooltip}">
									<!--tooltip works-->
									<xsl:value-of select="."/>
								</th>
							</xsl:for-each>
							<tr>
								<xsl:for-each select="td">
									<td>
										<xsl:value-of select="current()"/>
									</td>


								</xsl:for-each>
							</tr>
						</table><br></br>
						<!--<table>
							<xsl:for-each select="td">
								<tr>
									<th>
										<xsl:value-of select="@headerName"/>
									</th>
								</tr>
								<tr>
								<td>
									<xsl:value-of select="current()"/>
								</td>
								</tr>
							</xsl:for-each>
						</table>-->
						<div class="hdr">Role Summary:</div>
						Veeam proxies are a logical datamover component. There are two types of proxies: (<a href="https://helpcenter.veeam.com/docs/backup/vsphere/backup_proxy.html?zoom_highlight=proxy&amp;ver=110">Backup Proxies</a>, <a href="https://helpcenter.veeam.com/docs/backup/vsphere/cdp_proxy.html?ver=110">CDP Proxies</a>). Backup Proxies are further subdivided based on function or platform e.g. File Proxies for NAS-based backups, or different source hypervisors.
						<div class="i2">•	Backup proxies sit between source data (VMs or File Shares) and the backup repositories. Their role is to process backup jobs and deliver backup traffic to the repositories.</div>
						<div class="i3">
							o	VM Backup proxies can leverage different <a href="https://helpcenter.veeam.com/docs/backup/vsphere/transport_modes.html?ver=110">transport modes</a>.
						</div>
						<div class="i3">
							o	File Backup proxies can backup source data from manual or automated snapshot paths for <a href="https://helpcenter.veeam.com/docs/backup/vsphere/file_share_backup_nfs_share_advanced_settings.html?ver=110"> NFS</a>, <a href="https://helpcenter.veeam.com/docs/backup/vsphere/file_share_backup_smb_share_advanced_settings.html?ver=110">SMB</a>, Enterprise NAS filers. (Note: Managed servers do not leverage File Backup Proxies)
						</div>
						<div class="i3">o	Backup Proxies do not store or cache any data locally.</div>
						<div class="i2">•	CDP proxies process CDP policies and operate as data movers between source and target VMware hosts.</div>
						<div class="i3">
							o	CDP proxies require a <a href="https://helpcenter.veeam.com/docs/backup/vsphere/system_requirements.html?ver=110#vmware-cdp-proxy-server">local cache.</a>
						</div>
						<br></br>
						<div class="hdr">Notes:</div>
						<div class="i2">
							•	Review the “host” column and identify the host(s) that support <a href="https://helpcenter.veeam.com/docs/backup/vsphere/limiting_tasks.html?ver=110#task-limitation-for-components-with-several-roles">multiple</a> proxy roles:
						</div>
						<div class="i3">
							o	Roles can be combined providing you allocate <a href="https://helpcenter.veeam.com/docs/backup/vsphere/limiting_tasks.html?ver=110">enough resources</a>.
						</div>
						<div class="i4">	If roles are non-concurrent then allocate the max resources calculated across all supported roles.</div>
						<div class="i4">
								If roles are concurrent then allocate <a href="https://helpcenter.veeam.com/docs/backup/vsphere/limiting_tasks.html?ver=110#task-limitation-for-components-with-several-roles">enough</a> resources for all roles
						</div>
						<div class="i4">	Keep in mind that CDP proxies are constantly active and transferring data, therefore it is imperative to “reserve” enough resources for its role and add what is necessary the combined role(s).</div>
						<div class="i2">•	CDP proxies:</div>
						<div class="i3">
							o	Ensure <a href="https://helpcenter.veeam.com/docs/backup/vsphere/cdp_proxy.html?zoom_highlight=CDP+cache&amp;ver=110#vmware-cdp-proxy-cache">cache</a> is properly sized
						</div>
						<div class="i3">o	Flag CDP proxies with cache located on the C:\ drive as a potential risk.</div>
						<div class="i2">
							•	Compare assigned <a href="https://helpcenter.veeam.com/docs/backup/vsphere/limiting_tasks.html?ver=110">tasks</a> and core count and identify <a href="https://helpcenter.veeam.com/docs/backup/vsphere/limiting_tasks.html?ver=110#task-limitation-for-backup-proxies">oversubscription</a>.
						</div>
						<div class="i2">
							•	Confirm the RAM to core ratio is <a href="https://helpcenter.veeam.com/docs/backup/vsphere/system_requirements.html?ver=110#vmware-cdp-proxy-server">adequate</a>.
						</div>
						<div class="i2">
							•	Ensure there are enough resources for the base OS. (<a href="https://helpcenter.veeam.com/docs/backup/vsphere/system_requirements.html?ver=110#backup-proxy-server">backup proxy</a>, <a href="https://helpcenter.veeam.com/docs/backup/vsphere/system_requirements.html?ver=110#vmware-cdp-proxy-server">CDP proxy</a>)
						</div>
						<div class="i2">
							•	Check selected <a href="https://helpcenter.veeam.com/docs/backup/vsphere/transport_modes.html?zoom_highlight=transport+mode&amp;ver=110">transport mode</a> and highlight where <a href="https://helpcenter.veeam.com/docs/backup/vsphere/network_mode_failover.html?ver=110">failover to network mode</a> is disabled when jobs are failing or where failover to network is enabled for jobs to run slower than expected (NBD traffic could flow through the <a href="https://helpcenter.veeam.com/docs/backup/vsphere/select_backup_network.html?zoom_highlight=preferred+network&amp;ver=110">wrong network</a>).
						</div>
						<div class="i2">
							•	Hyper-V off host proxies should <a href="https://helpcenter.veeam.com/docs/backup/hyperv/offhost_backup_proxy.html?ver=110">match</a> protected Hyper-V host versions.
						</div>




						<h5>
							<a href="#top">Back To Top</a>
						</h5>
					</body>
				</html>
			</div>
		</div>

	</xsl:template>
</xsl:stylesheet>


