namespace clarivate_incites_export;

class EmployeeData
{
    public string EMPLID;
    public string EMPLOYEE_NBR;
    public string USERNAME;
    public string LAST_NAME;
    public string FIRST_NAME;
    public string EMAIL_ADDRESS;
    public string DEPARTMENT_CD;
    public string DEPARTMENT_DESCR;
    public string RESPONSIBILITY_CENTER_CD;
    public string RESPONSIBILITY_CENTER_DESCR;
    public string FACULTY_TENURE_STATUS_DESCR;
    public string JOB_KEY;
    public string JOB_TYPE;
    public string JOB_FAMILY;
    public string JOB_CLASS;
    public string BUILDING_NAME;
    public string ROOM_NBR;
}

record IdentifierData(
    string EMPLID,
    string USERNAME,
    string EMAIL,
    string IDENTIFIER_ID,
    string IDENTIFIER_VALUE
);

record OrcidData(string USERNAME, string ORCID);