<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<app name="GoldenDemo" ver="35" modder="Administrator.WIN-1GPQ0NALNPB.WORKGROUP" dt="03/13/24 15:00:47.464 " src_ver="34">
	<k name="tmservers">
		<k name="tms" ip="127.0.0.1" port="2402" retry="3" protocol="300"/>
	</k>
	<k name="runtime" v="batches"/>
	<k name="tmengine" cs="[secured]01930628f4dbdf93e45d3c603f6d1f3d2dce03a5120ec715b6d6d5ed0d3c2127c199eff68bcf199c470bb69b7539e966c5f3cab6d658c1c64d80cd35bd3be0148de756187d5e79e1b1d0c0ff7974ea052d6709eea4b358f8c96a1e7caea874e5b85d89497bca975d600768f1adaa35570260a15f3ccf70ad0c0c6c7f952e065c9a3cf09977cf807b7d7a36af482490d4ec3ad2a9db6dcd690af49da07208721fc6d60586f3388f2b7cac80aac299a710da42f2c2f29a782e84ae22641c1e3834bc[/secured]"/>
	<k name="tmadmin" cs="[secured]0164f2692454bd04355dc0b770a4805ef661feb065bcc7c8e828f3d357542536d05066287f293ead0fe180b6505c3be8bb52f8300d6fc28dd36a6bc204cc051e299eabafb0ee88ed9c185f337233e604dbde90c9d1268b47cef404ebe119fb6134370855938e3214bc0f16dd43b1a43fec47a8ec760a58d69b594b682df47e2cbcd21ad585876eb14a50131e6946b1205ef89d1ab062f44ec2e06f6e4837e71a45b126ab4627197eb04cf321f3b9837c37b0f7da5d686e07e54e13c50757ede725[/secured]"/>
	<k name="dco_GoldenDemo">
		<k name="setupdco" v="GoldenDemo.xml"/>
		<k name="rules" v="rules"/>
		<k name="imagefix" v="imagefix.ini"/>
		<k name="UseFPXML" v="True"/>
		<k name="fingerprintconn" cs="[secured]01f102a68dc10532113da2b7ba9c414976fd3497af7c517b31fc66acae60591fcc6d187aae62eb0ba29e299333980dad6b4e55b3b40a6146df7391a5801a3a3f2c2229ffdd117e6921bf08b9783cf61fbb4e9497c75cc34efbcfdb5861d5a459593d3e5fa10adbe4a1e42d01ae759c20ae4c43c284a9c4003bf8854f4eff5c3a8e24f3795472be354b82eb041d8f3522ac4ef6cf1f0f837e949992ca911e7ca65a30c9ea9a769abd2ad9ad81a869548618e2b31dd15b2d0d84cc5ef5f74a3357bd2b60c119e507f843f3f82f96a63fc649[/secured]"/>
		<k name="lookupdb" cs="[secured]01ecdcd3b7c0303acb884c1c59ddedab25d69d9f1a41409603dad9e03b519a38be94130c27624bb479b05d10cca36d223c94772b815e56c2c127511c18a457088a3dcc5e3383fb06608fc6390fe56b9395c861f600a3676e447a681f38b42e7f1729b5ba6b5d6c391d7c338ec4ea3b612de2eeedc7e0f36d9b571bde599de76bf0f5beef9ca7854bc2824cc8ecf398c74cca885fe929577e5b898eb37fc3489cea9f16dd83c1e09516193604da4b062883db038063deb1b30e7787723a5cea734c[/secured]"/>
		<k name="vscanimagedir" v="C:\Datacap\GoldenDemo\images\Input_SingleTIFFs"/>
		<k name="vscanimagedir_Multi" v="C:\Datacap\GoldenDemo\images\Input_MultiFormat"/>
		<k name="exportdb" cs="[secured]01ab06f25e3958533d2ae3860420e44fc97b8d81eda12055a359373fcabef5d13a1ad1545e58b810a33e4521d7afcd8bee5719fbfca252f431dcfa10a93a8aab80[/secured]"/>
		<k name="copyimagedir" v="C:\Datacap\GoldenDemo\images\Input_SingleTIFFs"/>
		<k name="SaveReportStatistics" v="True"/>
		<k name="locale" v=""/>
	</k>
	<k name="fingerprint" v="fingerprint"/>
	<k name="export" v="export"/>
	<k name="tasks">
		<k name="VScan" profile="ScanFromDisk_MultiFormat"/>
		<k name="Convert" profile="Convert"/>
		<k name="PageID" profile="PageID_VendorOCR_POC"/>
		<k name="Profiler" profile="Profiler"/>
		<k name="Export" profile="Export"/>
		<k name="NProfiler" profile="Profiler"/>
		<k name="NPageID" profile="PageID_VendorOCR_POC"/>
		<k name="FingerprintAdd" profile=""/>
	</k>
	<k name="values">
		<k name="gen">
			<k name="SettingsFile" v="[encoded].DJnAfX8E6FiV37a5q30yZDsLDvRFTFG4ZBhLCFv6NpD5H9qu4n6EvY3xk7C537Z1Zd0yY5Lp18g2gcG4b7QvCFv27b0IVE7C0X0EvQCUIBSLFuX5puDlK9c09f66wpG4VBhD3jQ27J0IS5ag0Wr[/encoded]"/>
			<k name="FileNetURL" v="[encoded].4n7AfE8Dw2vuFueEMj0ymDsG5OxBD47XjBhWCFt27W8p45aL0WqAfBGkYBSLBeAA6E9VN9bnDv7FT1G48BhR7zh27W4YiE6wDJm6P43xt7BzFuZ1ZeDleDs1DvK2ga7Y7BhJ3jf[/encoded]"/>
			<k name="FileNetUserID" v="[encoded].0WsAfB3xkFibBe41Zh[/encoded]"/>
		</k>
		<k name="adv">
			<k name="FileNetPassword" v="[secured]017e13e31cb468ccb3a8cabd3f2707aac51dfd8e912c0fd2812ef1c8da09862d9e011e5437a437148846ded0be721f3ec42f0e990a45f6ca8a75c7f2b42465d139[/secured]"/>
		</k>
	</k>
	<k name="BatchNew" v=""/>
	<k name="QBy" v="job"/>
	<k name="RoleMode" v="none"/>
	<k name="Authenticator" v="tma"/>
	<k name="AutoImportNTGroups" v="True"/>
	<k name="Audit" v="True"/>
	<k name="SaveDeletedBatchInfo" v="True"/>
	<k name="InheritJobPriority" v="False"/>
	<k name="rrslogdir" v="batches"/>
</app>