;--------------------------------------------
;-----Functions to manage window service-----

!macro _InstallService Name DisplayName ServiceType StartType BinPath TimeOut
    SimpleSC::ExistsService ${Name}
    Pop $0
    ${If} $0 == 0
        SimpleSC::StopService "${Name}" 1 ${TimeOut}
    Pop $0
        ${If} $0 != 0
            Abort "$(ServiceStopFailMessage) $0"
        ${EndIf}

        SimpleSC::RemoveService ${Name}
        Pop $0
        ${If} $0 != 0
            Push $0
            SimpleSC::GetErrorMessage
            Pop $1
            Abort "$(ServiceUninstallFailMessage) $0 $1"
        ${EndIf}
    ${EndIf}
    
    SimpleSC::InstallService "${Name}" "${DisplayName}" "${ServiceType}" "${StartType}" ${BinPath} "" "" ""
    Pop $0
    ${If} $0 != 0
        Push $0
        SimpleSC::GetErrorMessage
        Pop $1
        Abort "$(ServiceInstallFailMessage) $0 $1"
    ${EndIf}
!macroend

!macro _ConfigureService Name
    SimpleSC::ExistsService ${Name}
    Pop $0
    ${If} $0 == 0
        SimpleSC::SetServiceFailure ${Name} 0 "" "" 1 60000 1 60000 0 60000
        Pop $0
        ${If} $0 != 0
            Abort "$(ServiceConfigFailMessage) $0"
        ${EndIf}
    ${EndIf}
!macroend

!macro _StartService Name TimeOut
    SimpleSC::ExistsService ${Name}
    Pop $0
    ${If} $0 == 0
        SimpleSC::StartService "${Name}" "" ${TimeOut}
    Pop $0
        ${If} $0 != 0
            Abort "$(ServiceStartFailMessage) $0"
        ${EndIf}
    ${EndIf}
!macroend

!macro _StopService Name TimeOut
    SimpleSC::ExistsService ${Name}
    Pop $0
    ${If} $0 == 0
    SimpleSC::StopService "${SERVICE_NAME}" 1 ${TimeOut}
    Pop $0
        ${If} $0 != 0
            Abort "$(ServiceStopFailMessage) $0"
        ${EndIf}
        Sleep ${Sleep}
    ${EndIf}
!macroend

!macro _UninstallService Name TimeOut
    SimpleSC::ExistsService ${Name}
    Pop $0
    ${If} $0 == 0
    SimpleSC::ServiceIsStopped ${Name}
    Pop $0
    Pop $1
    ${If} $1 == 0
            SimpleSC::StopService "${SERVICE_NAME}" 1 ${TimeOut}
        Pop $0
            ${If} $0 != 0
                Abort "$(ServiceStopFailMessage) $0"
            ${EndIf}
        ${EndIf}
     
        SimpleSC::RemoveService ${Name}
        Pop $0
        ${If} $0 != 0
            Push $0
            SimpleSC::GetErrorMessage
            Pop $1
            Abort "$(ServiceUninstallFailMessage) $0 $1"
        ${EndIf}
    ${EndIf}
!macroend

!define InstallService '!insertmacro "_InstallService"'
!define StartService '!insertmacro "_StartService"'
!define StopService '!insertmacro "_StopService"'
!define UninstallService '!insertmacro "_UninstallService"'
!define ConfigureService '!insertmacro "_ConfigureService"'

;---END Functions to manage window service---

;--------------------------------------------
;-----Functions to manage sections-----

!define SEC_USELECTED  0
!define SEC_SELECTED   1
!define SEC_BOLD       8
!define SEC_RO         16
!define SEC_EXPAND     32
 
!macro SecSelectChange SecId
    Push $0
    SectionGetFlags ${SecId} $0
    IntOp $0 $0 ^ ${SEC_SELECTED}
    SectionSetFlags ${SecId} $0
    Pop $0
!macroend

!macro SecBoldChange SecId
    Push $0
    SectionGetFlags ${SecId} $0
    IntOp $0 $0 ^ ${SEC_BOLD}
    SectionSetFlags ${SecId} $0
    Pop $0
!macroend

!macro SecROChange SecId
    Push $0
    SectionGetFlags ${SecId} $0
    IntOp $0 $0 ^ ${SEC_RO}
    SectionSetFlags ${SecId} $0
    Pop $0
!macroend

!macro SecExpandChange SecId
    Push $0
    SectionGetFlags ${SecId} $0
    IntOp $0 $0 ^ ${SEC_EXPAND}
    SectionSetFlags ${SecId} $0
    Pop $0
!macroend
 

!define ChangeSectionSelectState '!insertmacro SecSelectChange'
!define ChangeSectionBoldState '!insertmacro SecBoldChange'
!define ChangeSectionReadOnlyState '!insertmacro SecROChange'
!define ChangeSectionExpandState '!insertmacro SecExpandChange'

;---END Functions to manage sections---