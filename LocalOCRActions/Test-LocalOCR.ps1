# Test-LocalOCR.ps1
$testScript = @"
using System;
using System.Collections.Generic;

public class TestDCOField
{
    public string Name { get; set; }
    public string Text { get; set; }
    public int VerificationStatus { get; set; }

    public TestDCOField(string name)
    {
        Name = name;
        Text = "";
        VerificationStatus = 0;
    }
}

public class TestDCODoc
{
    public string Type { get; set; }
    public int ObjectType() { return 1; }
}

public class TestDCOPage
{
    public string ID { get; set; }
    public string Type { get; set; }
    public string ImageName { get; set; }
    public int VerificationStatus { get; set; }
    public TestDCODoc Parent { get; set; }
    public Dictionary<string, TestDCOField> Fields = new Dictionary<string, TestDCOField>();

    public int ObjectType() { return 2; }

    public void CreateFields()
    {
        Fields.Clear();
        if (Type == "Signature_Form_1")
        {
            Fields["AccountHolderName"] = new TestDCOField("AccountHolderName");
            Fields["AccountNumber"] = new TestDCOField("AccountNumber");
        }
        else if (Type == "Form_Page_1")
        {
            Fields["EmployerName2"] = new TestDCOField("EmployerName2");
            Fields["AvrgMonthlyIncome"] = new TestDCOField("AvrgMonthlyIncome");
        }
        else if (Type == "National ID Front")
        {
            Fields["FullNameArabic"] = new TestDCOField("FullNameArabic");
            Fields["FamilyNameArabic"] = new TestDCOField("FamilyNameArabic");
            Fields["AddressArabic"] = new TestDCOField("AddressArabic");
            Fields["NationalIdNumber"] = new TestDCOField("NationalIdNumber");
            Fields["EmployerName"] = new TestDCOField("EmployerName");
        }
    }

    public object FindChild(string name)
    {
        if (Fields.ContainsKey(name)) return Fields[name];
        return null;
    }
}

public class TestRRLog
{
    public List<string> Messages = new List<string>();
    public void Write(string msg)
    {
        Messages.Add(msg);
        Console.WriteLine("[RRLOG] " + msg);
    }
}
"@

Add-Type -TypeDefinition $testScript -Language CSharp

$asmPath = "C:\Datacap\LocalOCRActions\LocalOCRActions.dll"
$asm = [System.Reflection.Assembly]::LoadFrom($asmPath)

$forms = @(
    @{ Name = "Signature Form"; Path = "C:\Datacap\Bob_Local_OCR_Datacap_Handoff_Final\Bob_Local_OCR_PageID_Handoff\Datacap_Reference_Only\forms\Signature_Form_Image_5.jpg" },
    @{ Name = "NPA Form"; Path = "C:\Datacap\Bob_Local_OCR_Datacap_Handoff_Final\Bob_Local_OCR_PageID_Handoff\Datacap_Reference_Only\forms\NPA_Form_Image_2.jpg" },
    @{ Name = "National ID"; Path = "C:\Datacap\Bob_Local_OCR_Datacap_Handoff_Final\Bob_Local_OCR_PageID_Handoff\Datacap_Reference_Only\forms\National_ID_Image_6.jpg" }
)

Write-Host "================================================================"
Write-Host "LOCAL OCR ACTIONS VALIDATION SUITE"
Write-Host "================================================================"

foreach ($item in $forms) {
    Write-Host "`n>>> Processing Form: $($item.Name)"
    Write-Host "    Image: $($item.Path)"

    $page = New-Object TestDCOPage
    $page.ID = "TM000001"
    $page.Type = "Other"
    $page.ImageName = $item.Path
    $page.Parent = New-Object TestDCODoc

    $log = New-Object TestRRLog

    # 1. PageIdentify
    $idAction = New-Object LocalOCRActions.LocalOCR_PageIdentify
    $idAction.DatacapRRLog = $log
    $idAction.DatacapRRCurrentDCO = $page

    $idRc = $idAction.Execute("")
    Write-Host "    [Action 1] LocalOCR_PageIdentify RC = $idRc"
    Write-Host "    - Page Type: $($page.Type)"
    Write-Host "    - Document Type: $($page.Parent.Type)"
    Write-Host "    - Page VerificationStatus: $($page.VerificationStatus)"

    # 2. ExtractFields
    $extAction = New-Object LocalOCRActions.LocalOCR_ExtractFields
    $extAction.DatacapRRLog = $log
    $extAction.DatacapRRCurrentDCO = $page

    $extRc = $extAction.Execute("")
    Write-Host "    [Action 2] LocalOCR_ExtractFields RC = $extRc"
    Write-Host "    - Fields Extracted:"
    foreach ($k in $page.Fields.Keys) {
        $f = $page.Fields[$k]
        Write-Host "        * $($f.Name): '$($f.Text)' (VerificationStatus=$($f.VerificationStatus))"
    }
}
Write-Host "`n================================================================"
Write-Host "TEST SUITE COMPLETED"
Write-Host "================================================================"
