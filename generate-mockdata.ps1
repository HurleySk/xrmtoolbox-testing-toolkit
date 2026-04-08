<#
.SYNOPSIS
    Analyzes an XrmToolBox plugin's source code and generates starter mock data + control inventory.

.DESCRIPTION
    Scans a plugin project directory for:
    - UI controls in *.Designer.cs files
    - SDK request types (new *Request)
    - SDK operations (RetrieveMultiple, Create, etc.)
    - Entity name references
    - FetchXML entity references

    Produces two output files:
    - test-mockdata.json: starter mock data for the XrmToolBox Test Harness
    - test-control-inventory.json: UI control names, types, and interaction categories

.PARAMETER PluginSourceDir
    Path to the plugin project root (where the .csproj lives). Required.

.PARAMETER OutputPath
    Path for the generated mock data JSON. Default: <PluginSourceDir>/test-mockdata.json

.PARAMETER ControlInventoryPath
    Path for the generated control inventory JSON. Default: <PluginSourceDir>/test-control-inventory.json

.EXAMPLE
    .\generate-mockdata.ps1 -PluginSourceDir "C:\repos\MyPlugin"
    .\generate-mockdata.ps1 -PluginSourceDir "C:\repos\MyPlugin" -OutputPath "C:\test\mockdata.json"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginSourceDir,

    [string]$OutputPath,

    [string]$ControlInventoryPath
)

$ErrorActionPreference = "Stop"

# Validate input
if (-not (Test-Path $PluginSourceDir)) {
    Write-Error "Plugin source directory not found: $PluginSourceDir"
    exit 1
}

# Resolve to absolute path
$PluginSourceDir = (Resolve-Path $PluginSourceDir).Path

# Set defaults
if (-not $OutputPath) { $OutputPath = Join-Path $PluginSourceDir "test-mockdata.json" }
if (-not $ControlInventoryPath) { $ControlInventoryPath = Join-Path $PluginSourceDir "test-control-inventory.json" }

Write-Host "=== XrmToolBox Plugin Analyzer ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Plugin source: $PluginSourceDir" -ForegroundColor White
Write-Host ""

# =============================================================================
# Phase 1: Discover UI Controls
# =============================================================================
Write-Host "--- Phase 1: Discovering UI Controls ---" -ForegroundColor Yellow

$designerFiles = Get-ChildItem -Path $PluginSourceDir -Filter "*Control.Designer.cs" -Recurse |
    Where-Object { $_.Name -notlike "Resources.Designer.cs" }

$controls = @()

foreach ($file in $designerFiles) {
    $content = Get-Content $file.FullName -Raw
    $matches = [regex]::Matches($content, 'this\.(\w+)\s*=\s*new\s+System\.Windows\.Forms\.(\w+)\(')
    foreach ($m in $matches) {
        $name = $m.Groups[1].Value
        $type = $m.Groups[2].Value

        # Classify by prefix
        $prefix = ""
        $category = "other"
        if ($name -match '^(btn)') { $prefix = "btn"; $category = "action" }
        elseif ($name -match '^(dgv)') { $prefix = "dgv"; $category = "data-display" }
        elseif ($name -match '^(cbo|cmb)') { $prefix = $Matches[1]; $category = "input-dropdown" }
        elseif ($name -match '^(txt)') { $prefix = "txt"; $category = "input-text" }
        elseif ($name -match '^(chk)') { $prefix = "chk"; $category = "input-toggle" }
        elseif ($name -match '^(nud)') { $prefix = "nud"; $category = "input-numeric" }
        elseif ($name -match '^(rtb)') { $prefix = "rtb"; $category = "input-text" }
        elseif ($name -match '^(lbl)') { $prefix = "lbl"; $category = "feedback" }
        elseif ($name -match '^(progress)') { $prefix = "progress"; $category = "feedback" }
        elseif ($name -match '^(tab)') { $prefix = "tab"; $category = "navigation" }
        elseif ($name -match '^(grp)') { $prefix = "grp"; $category = "layout" }
        elseif ($name -match '^(split)') { $prefix = "split"; $category = "layout" }
        elseif ($name -match '^(col)') { $prefix = "col"; $category = "column" }
        elseif ($name -match '^(pnl|panel)') { $prefix = "pnl"; $category = "layout" }
        elseif ($name -match '^(mnu|menu|tsm|toolStrip)') { $prefix = "menu"; $category = "menu" }

        $controls += [PSCustomObject]@{
            Name     = $name
            Type     = $type
            Category = $category
            Prefix   = $prefix
        }
    }
}

# Summary
$buttons = @($controls | Where-Object { $_.Category -eq "action" })
$grids = @($controls | Where-Object { $_.Category -eq "data-display" })
$inputDropdowns = @($controls | Where-Object { $_.Category -eq "input-dropdown" })
$inputTexts = @($controls | Where-Object { $_.Category -eq "input-text" })
$inputToggles = @($controls | Where-Object { $_.Category -eq "input-toggle" })
$inputNumerics = @($controls | Where-Object { $_.Category -eq "input-numeric" })
$feedbacks = @($controls | Where-Object { $_.Category -eq "feedback" })
$navs = @($controls | Where-Object { $_.Category -eq "navigation" })
$layouts = @($controls | Where-Object { $_.Category -in "layout", "column", "menu" })

Write-Host "  Total controls: $($controls.Count)" -ForegroundColor Green
if ($buttons.Count -gt 0) { Write-Host "  Buttons ($($buttons.Count)):     $($buttons.Name -join ', ')" -ForegroundColor White }
if ($grids.Count -gt 0) { Write-Host "  Grids ($($grids.Count)):       $($grids.Name -join ', ')" -ForegroundColor White }
if ($inputDropdowns.Count -gt 0) { Write-Host "  Dropdowns ($($inputDropdowns.Count)):   $($inputDropdowns.Name -join ', ')" -ForegroundColor White }
if ($inputTexts.Count -gt 0) { Write-Host "  TextBoxes ($($inputTexts.Count)):   $($inputTexts.Name -join ', ')" -ForegroundColor White }
if ($inputToggles.Count -gt 0) { Write-Host "  CheckBoxes ($($inputToggles.Count)):  $($inputToggles.Name -join ', ')" -ForegroundColor White }
if ($inputNumerics.Count -gt 0) { Write-Host "  Numerics ($($inputNumerics.Count)):    $($inputNumerics.Name -join ', ')" -ForegroundColor White }
if ($feedbacks.Count -gt 0) { Write-Host "  Feedback ($($feedbacks.Count)):    $($feedbacks.Name -join ', ')" -ForegroundColor White }
if ($navs.Count -gt 0) { Write-Host "  Navigation ($($navs.Count)):  $($navs.Name -join ', ')" -ForegroundColor White }
if ($layouts.Count -gt 0) { Write-Host "  Layout ($($layouts.Count)):      (grp, split, col, panel, menu)" -ForegroundColor DarkGray }
Write-Host ""

# =============================================================================
# Phase 2: Discover SDK Request Types
# =============================================================================
Write-Host "--- Phase 2: Discovering SDK Request Types ---" -ForegroundColor Yellow

$csFiles = Get-ChildItem -Path $PluginSourceDir -Filter "*.cs" -Recurse |
    Where-Object { $_.Name -notlike "*.Designer.cs" -and $_.FullName -notlike "*\obj\*" -and $_.FullName -notlike "*\bin\*" }

# Known request type -> full namespace mapping
$knownRequestTypes = @{
    "WhoAmIRequest"                  = "Microsoft.Crm.Sdk.Messages.WhoAmIRequest"
    "RetrieveAllEntitiesRequest"     = "Microsoft.Xrm.Sdk.Messages.RetrieveAllEntitiesRequest"
    "RetrieveEntityRequest"          = "Microsoft.Xrm.Sdk.Messages.RetrieveEntityRequest"
    "RetrieveAttributeRequest"       = "Microsoft.Xrm.Sdk.Messages.RetrieveAttributeRequest"
    "ExecuteMultipleRequest"         = "Microsoft.Xrm.Sdk.Messages.ExecuteMultipleRequest"
    "AssociateRequest"               = "Microsoft.Xrm.Sdk.Messages.AssociateRequest"
    "DisassociateRequest"            = "Microsoft.Xrm.Sdk.Messages.DisassociateRequest"
    "RetrieveRelationshipRequest"    = "Microsoft.Xrm.Sdk.Messages.RetrieveRelationshipRequest"
    "RetrieveAllOptionSetsRequest"   = "Microsoft.Xrm.Sdk.Messages.RetrieveAllOptionSetsRequest"
    "RetrieveOptionSetRequest"       = "Microsoft.Xrm.Sdk.Messages.RetrieveOptionSetRequest"
    "CreateRequest"                  = "Microsoft.Xrm.Sdk.Messages.CreateRequest"
    "UpdateRequest"                  = "Microsoft.Xrm.Sdk.Messages.UpdateRequest"
    "DeleteRequest"                  = "Microsoft.Xrm.Sdk.Messages.DeleteRequest"
    "RetrieveRequest"                = "Microsoft.Xrm.Sdk.Messages.RetrieveRequest"
    "RetrieveMultipleRequest"        = "Microsoft.Xrm.Sdk.Messages.RetrieveMultipleRequest"
    "SetStateRequest"                = "Microsoft.Crm.Sdk.Messages.SetStateRequest"
    "AssignRequest"                  = "Microsoft.Crm.Sdk.Messages.AssignRequest"
    "PublishAllXmlRequest"           = "Microsoft.Crm.Sdk.Messages.PublishAllXmlRequest"
    "ExportSolutionRequest"          = "Microsoft.Crm.Sdk.Messages.ExportSolutionRequest"
    "ImportSolutionRequest"          = "Microsoft.Crm.Sdk.Messages.ImportSolutionRequest"
}

$requestTypes = @()
$customActions = @()

foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw

    # Typed request classes: new WhoAmIRequest, new RetrieveAllEntitiesRequest, etc.
    $typedMatches = [regex]::Matches($content, 'new\s+(\w+Request)\b')
    foreach ($m in $typedMatches) {
        $shortName = $m.Groups[1].Value
        # Skip OrganizationRequest and OrganizationRequestCollection
        if ($shortName -eq "OrganizationRequest" -or $shortName -eq "OrganizationRequestCollection") { continue }
        $fullName = if ($knownRequestTypes.ContainsKey($shortName)) { $knownRequestTypes[$shortName] } else { "Microsoft.Crm.Sdk.Messages.$shortName" }
        $responseName = $fullName -replace 'Request$', 'Response'
        $requestTypes += [PSCustomObject]@{
            ShortName    = $shortName
            FullTypeName = $fullName
            ResponseType = $responseName
            SourceFile   = $file.Name
        }
    }

    # Custom actions: new OrganizationRequest("ActionName")
    $customMatches = [regex]::Matches($content, 'new\s+OrganizationRequest\(\s*"(\w+)"\s*\)')
    foreach ($m in $customMatches) {
        $actionName = $m.Groups[1].Value
        $customActions += [PSCustomObject]@{
            ActionName = $actionName
            SourceFile = $file.Name
        }
    }
}

# Deduplicate
$requestTypes = $requestTypes | Sort-Object ShortName -Unique

if ($requestTypes.Count -gt 0) {
    Write-Host "  Typed requests ($($requestTypes.Count)):" -ForegroundColor Green
    foreach ($rt in $requestTypes) {
        Write-Host "    $($rt.ShortName) -> $($rt.FullTypeName)  ($($rt.SourceFile))" -ForegroundColor White
    }
}
if ($customActions.Count -gt 0) {
    $customActions = $customActions | Sort-Object ActionName -Unique
    Write-Host "  Custom actions ($($customActions.Count)):" -ForegroundColor Green
    foreach ($ca in $customActions) {
        Write-Host "    OrganizationRequest(`"$($ca.ActionName)`")  ($($ca.SourceFile))" -ForegroundColor White
    }
}
if ($requestTypes.Count -eq 0 -and $customActions.Count -eq 0) {
    Write-Host "  No SDK request types found" -ForegroundColor DarkGray
}
Write-Host ""

# =============================================================================
# Phase 3: Discover SDK Operations
# =============================================================================
Write-Host "--- Phase 3: Discovering SDK Operations ---" -ForegroundColor Yellow

$operations = @()
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw
    $opMatches = [regex]::Matches($content, '\.(RetrieveMultiple|Retrieve|Create|Update|Delete|Associate|Disassociate)\s*\(')
    foreach ($m in $opMatches) {
        $operations += $m.Groups[1].Value
    }
}
$operations = $operations | Sort-Object -Unique

if ($operations.Count -gt 0) {
    Write-Host "  Operations: $($operations -join ', ')" -ForegroundColor Green
} else {
    Write-Host "  No direct SDK operations found (may use Execute only)" -ForegroundColor DarkGray
}
Write-Host ""

# =============================================================================
# Phase 4: Discover Entity Names
# =============================================================================
Write-Host "--- Phase 4: Discovering Entity Names ---" -ForegroundColor Yellow

# Known Dataverse entity names (high confidence)
$knownEntities = @(
    "account", "contact", "lead", "opportunity", "systemuser", "team",
    "businessunit", "solution", "entity", "attribute", "relationship",
    "role", "workflow", "annotation", "activitypointer", "email", "task",
    "phonecall", "appointment", "incident", "knowledgearticle", "queue",
    "queueitem", "organization", "savedquery", "userquery", "report",
    "connection", "connectionrole", "transactioncurrency", "subject",
    "product", "pricelevel", "productpricelevel", "quote", "salesorder",
    "invoice", "campaign", "list", "activityparty", "letter", "fax",
    "socialactivity", "recurringappointmentmaster", "goalrollupquery",
    "goal", "metric", "rollupfield", "kbarticle", "kbarticletemplate",
    "publisher", "pluginassembly", "plugintype", "sdkmessage",
    "sdkmessageprocessingstep", "serviceendpoint", "webresource",
    "sitemap", "systemform", "savedqueryvisualization", "customcontrol"
)

# Common programming strings to exclude
$excludeStrings = @(
    "string", "null", "true", "false", "error", "message", "value",
    "count", "name", "type", "index", "query", "result", "format",
    "path", "file", "text", "data", "test", "code", "info", "item",
    "list", "config", "action", "method", "class", "event", "table",
    "column", "field", "source", "target", "status", "state",
    "response", "request", "connection", "server", "client",
    "version", "description", "label", "title", "object", "model",
    "service", "none", "empty", "default", "system", "user",
    "timeout", "cancel", "async", "task", "thread", "process",
    "loading", "complete", "success", "failed", "warning", "debug",
    "trace", "log", "output", "input", "key", "guid", "date",
    "time", "number", "boolean", "integer", "decimal", "double",
    "float", "long", "short", "byte", "char", "void", "var",
    "new", "get", "set", "add", "remove", "update", "delete",
    "create", "retrieve", "select", "where", "from", "order",
    "group", "join", "inner", "outer", "left", "right", "and",
    "not", "like", "between", "exists", "insert", "into", "values",
    "fetch", "xml", "json", "csv", "html", "url", "uri", "http",
    "https", "content", "header", "body", "param", "args",
    "exception", "stack", "catch", "try", "finally", "throw",
    "return", "break", "continue", "switch", "case", "while",
    "for", "each", "with", "this", "base", "self", "static",
    "public", "private", "protected", "internal", "abstract",
    "virtual", "override", "readonly", "const", "enum", "struct",
    "interface", "namespace", "using", "import", "export",
    "assembly", "resource", "property", "attribute", "entity",
    "record", "form", "view", "plugin", "control", "window",
    "dialog", "panel", "button", "checkbox", "textbox", "label",
    "grid", "tab", "menu", "toolbar", "statusbar", "progressbar",
    "image", "icon", "color", "font", "size", "width", "height",
    "visible", "enabled", "checked", "selected", "focused",
    "dock", "anchor", "margin", "padding", "border", "align",
    "top", "bottom", "left_val", "right_val", "center", "fill",
    "stretch", "wrap", "scroll", "auto", "manual", "custom",
    "endpoint", "network", "socket", "throttl", "underlying",
    "page", "batch", "retry", "retries", "parallel", "concurrent",
    "disposed", "canceled", "cancelled", "aggregate", "inner_error",
    "operation", "duration", "elapsed", "remaining", "total",
    "minimum", "maximum", "average", "percent", "progress",
    "complete", "pending", "running", "stopped", "paused",
    "separator", "delimiter", "newline", "whitespace", "pattern",
    "prefix", "suffix", "extension", "filename", "directory",
    "backup", "restore", "archive", "compress", "extract",
    "encode", "decode", "encrypt", "decrypt", "hash", "token",
    "credential", "permission", "access", "deny", "allow",
    "ownerid", "accountid", "contactid", "opportunityid",
    "relatedcontactid", "accountcontact"
)

$entityNames = @()
foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw
    $strMatches = [regex]::Matches($content, '"([a-z][a-z_]{2,30})"')
    foreach ($m in $strMatches) {
        $candidate = $m.Groups[1].Value
        if ($candidate -notin $excludeStrings) {
            $entityNames += $candidate
        }
    }
}

# =============================================================================
# Phase 5: Discover FetchXML Entities
# =============================================================================
Write-Host "--- Phase 5: Discovering FetchXML Entities ---" -ForegroundColor Yellow

foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw
    $fetchMatches = [regex]::Matches($content, '<entity\s+name\s*=\s*[""''](\w+)[""'']')
    foreach ($m in $fetchMatches) {
        $entityNames += $m.Groups[1].Value
    }
}

# Deduplicate and sort
$entityNames = $entityNames | Sort-Object -Unique

# Split into high-confidence (known) and possible
$confirmedEntities = @($entityNames | Where-Object { $_ -in $knownEntities })
$possibleEntities = @($entityNames | Where-Object { $_ -notin $knownEntities })

if ($confirmedEntities.Count -gt 0) {
    Write-Host "  Confirmed entities ($($confirmedEntities.Count)): $($confirmedEntities -join ', ')" -ForegroundColor Green
}
if ($possibleEntities.Count -gt 0) {
    Write-Host "  Possible entities ($($possibleEntities.Count)):  $($possibleEntities -join ', ')" -ForegroundColor DarkYellow
}
if ($confirmedEntities.Count -eq 0 -and $possibleEntities.Count -eq 0) {
    Write-Host "  No entity names found" -ForegroundColor DarkGray
}
Write-Host ""

# Combine all entity names for mock data generation (include both confirmed and possible)
$allEntities = $confirmedEntities + $possibleEntities

# =============================================================================
# Phase 6: Generate mockdata.json
# =============================================================================
Write-Host "--- Phase 6: Generating Mock Data ---" -ForegroundColor Yellow

$responses = [System.Collections.ArrayList]::new()

# Always include WhoAmI
$null = $responses.Add(@{
    operation   = "Execute"
    description = "WhoAmI response"
    match       = @{ requestType = "Microsoft.Crm.Sdk.Messages.WhoAmIRequest" }
    response    = @{
        responseType = "Microsoft.Crm.Sdk.Messages.WhoAmIResponse"
        results      = @{
            UserId         = [guid]::NewGuid().ToString()
            OrganizationId = [guid]::NewGuid().ToString()
            BusinessUnitId = [guid]::NewGuid().ToString()
        }
    }
})

# Add Execute responses for each discovered request type (except WhoAmI, already added)
foreach ($rt in $requestTypes) {
    if ($rt.ShortName -eq "WhoAmIRequest") { continue }
    if ($rt.ShortName -eq "ExecuteMultipleRequest") {
        # ExecuteMultiple returns ExecuteMultipleResponse with Responses collection
        $null = $responses.Add(@{
            operation   = "Execute"
            description = "Mock $($rt.ShortName)"
            match       = @{ requestType = $rt.FullTypeName }
            response    = @{
                responseType = $rt.ResponseType
                results      = @{ }
            }
        })
        continue
    }
    if ($rt.ShortName -eq "AssociateRequest") {
        # AssociateRequest goes through Associate method, not Execute
        continue
    }
    if ($rt.ShortName -eq "DisassociateRequest") {
        continue
    }
    if ($rt.ShortName -eq "RetrieveAllEntitiesRequest") {
        # Build entity metadata stubs for all discovered entities
        $metadataEntries = @()
        foreach ($eName in $allEntities) {
            $eDisplayName = (Get-Culture).TextInfo.ToTitleCase(($eName -replace '_', ' '))
            $metadataEntries += [ordered]@{
                logicalName          = $eName
                schemaName           = ($eDisplayName -replace ' ', '')
                displayName          = $eDisplayName
                displayCollectionName = "${eDisplayName}s"
                entitySetName        = "${eName}s"
                primaryIdAttribute   = "${eName}id"
                primaryNameAttribute = "name"
            }
        }
        # If no entities were discovered, add a few common defaults
        if ($metadataEntries.Count -eq 0) {
            $metadataEntries = @(
                [ordered]@{ logicalName = "account"; schemaName = "Account"; displayName = "Account"; primaryIdAttribute = "accountid"; primaryNameAttribute = "name" },
                [ordered]@{ logicalName = "contact"; schemaName = "Contact"; displayName = "Contact"; primaryIdAttribute = "contactid"; primaryNameAttribute = "fullname" }
            )
        }
        $null = $responses.Add(@{
            operation   = "Execute"
            description = "Mock $($rt.ShortName) - returns metadata for discovered entities"
            match       = @{ requestType = $rt.FullTypeName }
            response    = @{
                responseType   = $rt.ResponseType
                entityMetadata = $metadataEntries
                results        = @{ }
            }
        })
        continue
    }
    if ($rt.ShortName -eq "RetrieveEntityRequest") {
        # Return metadata for a single entity (first discovered, or account as default)
        $singleEntity = if ($allEntities.Count -gt 0) { $allEntities[0] } else { "account" }
        $singleDisplayName = (Get-Culture).TextInfo.ToTitleCase(($singleEntity -replace '_', ' '))
        $null = $responses.Add(@{
            operation   = "Execute"
            description = "Mock $($rt.ShortName) - returns metadata for $singleEntity"
            match       = @{ requestType = $rt.FullTypeName }
            response    = @{
                responseType   = $rt.ResponseType
                entityMetadata = [ordered]@{
                    logicalName          = $singleEntity
                    schemaName           = ($singleDisplayName -replace ' ', '')
                    displayName          = $singleDisplayName
                    primaryIdAttribute   = "${singleEntity}id"
                    primaryNameAttribute = "name"
                    attributes           = @(
                        [ordered]@{ logicalName = "${singleEntity}id"; displayName = "$singleDisplayName ID"; attributeType = "Uniqueidentifier" },
                        [ordered]@{ logicalName = "name"; displayName = "Name"; attributeType = "String" }
                    )
                }
                results        = @{ }
            }
        })
        continue
    }

    # Generic Execute response
    $null = $responses.Add(@{
        operation   = "Execute"
        description = "Mock $($rt.ShortName)"
        match       = @{ requestType = $rt.FullTypeName }
        response    = @{
            responseType = $rt.ResponseType
            results      = @{ }
        }
    })
}

# Add Execute responses for custom actions
foreach ($ca in $customActions) {
    $null = $responses.Add(@{
        operation   = "Execute"
        description = "Mock custom action: $($ca.ActionName)"
        match       = @{ requestType = $ca.ActionName }
        response    = @{
            results = @{ }
        }
    })
}

# Add RetrieveMultiple responses for each discovered entity
foreach ($entityName in $allEntities) {
    $entityId = [guid]::NewGuid().ToString()
    # Build a simple display name from entity name
    $displayName = (Get-Culture).TextInfo.ToTitleCase(($entityName -replace '_', ' '))

    $null = $responses.Add(@{
        operation   = "RetrieveMultiple"
        description = "Mock $entityName data"
        match       = @{ entityName = $entityName }
        response    = @{
            entities       = @(
                @{
                    logicalName = $entityName
                    id          = $entityId
                    attributes  = [ordered]@{
                        "$($entityName)id" = $entityId
                        name               = "Test $displayName"
                    }
                }
            )
            moreRecords      = $false
            totalRecordCount = -1
        }
    })
}

# Wildcard fallbacks
$null = $responses.Add(@{
    operation   = "RetrieveMultiple"
    description = "Catch-all: return empty result set for unmatched queries"
    match       = @{ "*" = "*" }
    response    = @{ entities = @(); moreRecords = $false; totalRecordCount = -1 }
})

$null = $responses.Add(@{
    operation   = "Create"
    description = "Catch-all: return a new GUID for any Create"
    match       = @{ "*" = "*" }
    response    = @{ id = [guid]::NewGuid().ToString() }
})

$null = $responses.Add(@{
    operation   = "Update"
    description = "Catch-all: no-op for any Update"
    match       = @{ "*" = "*" }
    response    = @{ }
})

$null = $responses.Add(@{
    operation   = "Delete"
    description = "Catch-all: no-op for any Delete"
    match       = @{ "*" = "*" }
    response    = @{ }
})

$null = $responses.Add(@{
    operation   = "Associate"
    description = "Catch-all: no-op for any Associate"
    match       = @{ "*" = "*" }
    response    = @{ }
})

$null = $responses.Add(@{
    operation   = "Disassociate"
    description = "Catch-all: no-op for any Disassociate"
    match       = @{ "*" = "*" }
    response    = @{ }
})

$mockConfig = [ordered]@{
    settings  = [ordered]@{
        throwIfUnmatched = $false
        defaultDelay     = 0
    }
    responses = $responses
}

# Write mock data JSON
$mockJson = $mockConfig | ConvertTo-Json -Depth 10
$mockJson | Out-File -FilePath $OutputPath -Encoding utf8
Write-Host "  Mock data written to: $OutputPath" -ForegroundColor Green
Write-Host "  Response entries: $($responses.Count)" -ForegroundColor White

# =============================================================================
# Phase 7: Generate control inventory JSON
# =============================================================================
Write-Host ""
Write-Host "--- Phase 7: Writing Control Inventory ---" -ForegroundColor Yellow

$inventory = [ordered]@{
    pluginSourceDir = $PluginSourceDir
    generatedAt     = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    controls        = @($controls | ForEach-Object {
        [ordered]@{
            name     = $_.Name
            type     = $_.Type
            category = $_.Category
            prefix   = $_.Prefix
        }
    })
    summary         = [ordered]@{
        total      = $controls.Count
        buttons    = $buttons.Count
        grids      = $grids.Count
        dropdowns  = $inputDropdowns.Count
        textBoxes  = $inputTexts.Count
        checkBoxes = $inputToggles.Count
        numerics   = $inputNumerics.Count
        feedback   = $feedbacks.Count
        navigation = $navs.Count
        layout     = $layouts.Count
    }
    actionButtons   = @($buttons | ForEach-Object { $_.Name })
    primaryAction   = ""
}

# Heuristic: first button that isn't stop/cancel/close/browse
$skipButtons = @("btnStop", "btnCancel", "btnClose", "btnBrowse", "btnBrowseCsv", "btnBrowseFile")
$primaryBtn = $buttons | Where-Object { $_.Name -notin $skipButtons } | Select-Object -First 1
if ($primaryBtn) {
    $inventory.primaryAction = $primaryBtn.Name
}

$inventoryJson = $inventory | ConvertTo-Json -Depth 10
$inventoryJson | Out-File -FilePath $ControlInventoryPath -Encoding utf8
Write-Host "  Control inventory written to: $ControlInventoryPath" -ForegroundColor Green
if ($inventory.primaryAction) {
    Write-Host "  Primary action button: $($inventory.primaryAction)" -ForegroundColor White
}

# =============================================================================
# Summary
# =============================================================================
Write-Host ""
Write-Host "=== Analysis Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Generated files:" -ForegroundColor White
Write-Host "  Mock data:         $OutputPath" -ForegroundColor Green
Write-Host "  Control inventory: $ControlInventoryPath" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Review test-mockdata.json and add any plugin-specific response data" -ForegroundColor White
Write-Host "  2. Review test-control-inventory.json for the UI control map" -ForegroundColor White
Write-Host "  3. Launch the test harness:" -ForegroundColor White
Write-Host "     XrmToolBox.TestHarness.exe --plugin `"path\to\Plugin.dll`" --mockdata `"$OutputPath`" --screenshots `"./screenshots`" --record `"calls.json`"" -ForegroundColor Gray
