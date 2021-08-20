select
ude.EMPLID,
ude.EMPLOYEE_NBR,
ude.USERNAME,
ude.LAST_NAME,
ude.FIRST_NAME,
ude.EMAIL_ADDRESS,
udd.DEPARTMENT_CD,
udd.DEPARTMENT_DESCR,
urc.RESPONSIBILITY_CENTER_CD,
urc.RESPONSIBILITY_CENTER_DESCR,
ude.FACULTY_TENURE_STATUS_DESCR

from UD_DATA.PY_EMPLOYMENT pye
join UD_DATA.UD_EMPLOYEE ude on pye.EMPLOYEE_KEY = ude.EMPLOYEE_KEY
join UD_DATA.UD_EMPLOYEE_FULL_PART_TIME efpt on pye.EMPLOYEE_FULL_PART_TIME_KEY = efpt.EMPLOYEE_FULL_PART_TIME_KEY
join UD_DATA.UD_ASSIGNMENT_STATUS uas on pye.ASSIGNMENT_STATUS_KEY = uas.ASSIGNMENT_STATUS_KEY
join UD_DATA.UD_DEPARTMENT udd on pye.DEPARTMENT_KEY = udd.DEPARTMENT_KEY
join UD_DATA.UD_RESPONSIBILITY_CENTER urc on udd.RESPONSIBILITY_CENTER_CD = urc.RESPONSIBILITY_CENTER_CD
join UD_DATA.UD_JOB udj on pye.JOB_KEY = udj.JOB_KEY
join UD_DATA.UD_CALENDAR cal on pye.CALENDAR_KEY = cal.CALENDAR_KEY

where cal.CALENDAR_KEY = SYS_CONTEXT ('G$CONTEXT', 'PYM_CU_CAL_K_0000')
and udd.current_flg = 1 and udj.current_flg = 1 and urc.current_flg = 1
and (
    -- SSoE
    /*
    -- Filtered by Excluded (12) (
        Faculty (Job Type) + Adjunct Assistant (Job Family), 
        Faculty (Job Type) + Adjunct Associate (Job Family), 
        Faculty (Job Type) + Visiting Research (Job Family), 
        Faculty (Job Type) + Adjunct Research (Job Family), 
        Faculty (Job Type) + Adjunct Research Assistant (Job Family), 
        Faculty (Job Type) + Adjunct (Job Family), 
        Faculty (Job Type) + Lecturer (Job Family), 
        Faculty (Job Type) + Lecturer (Job Family), 
        Faculty (Job Type) + Scholar (Job Family), 
        Faculty (Job Type) + Instructor (Job Family), 
        Post Doctoral (Job Type), 
        Academic (Job Type)
        ), ASSIGNMENT_STATUS (is Terminated between July 2019 and June 2020 or Active), RESPONSIBILITY_CENTER_DESCR (is Swanson School of Engineering)
    */
    (urc.RESPONSIBILITY_CENTER_CD = 23 
    and udj.JOB_TYPE = 'Faculty'
    and (uas.ASSIGNMENT_STATUS_KEY not in (17, 18) or
        (uas.ASSIGNMENT_STATUS_KEY in (17, 18) and ude.last_day_worked_dt >= '01-JUL-19'))
    and udj.JOB_FAMILY not in 
        ('Adjunct Assistant', 'Adjunct Associate', 'Visiting Research', 'Adjunct Research', 'Adjunct Research Assistant',
        'Adjunct', 'Lecturer', 'Scholar', 'Instructor'))
    or
    -- Dental Medicine
    (urc.RESPONSIBILITY_CENTER_CD = 31 and udj.JOB_TYPE in ('Academic', 'Faculty', 'Post Doctoral'))
    or
    -- Pharmacy
    /* Excluded (13) (
        Post Doctoral (Job Type), 
        Academic (Job Type), 
        Faculty (Job Type) + Lecturer (Job Family), 
        Faculty (Job Type) + Scholar (Job Family), 
        Faculty (Job Type) + Professor (Job Family) + Adjunct Assistant (Job Class), 
        Faculty (Job Type) + Professor (Job Family) + Adjunct (Job Class), 
        Faculty (Job Type) + Professor (Job Family) + Adjunct Associate (Job Class), 
        Faculty (Job Type) + Professor (Job Family) + Research Assistant (Job Class), 
        Faculty (Job Type) + Professor (Job Family) + Clinical Assistant (Job Class), 
        Faculty (Job Type) + Professor (Job Family) + Clinical Associate (Job Class), 
        Faculty (Job Type) + Professor (Job Family) + Distinguished Service (Job Class), 
        Faculty (Job Type) + Professor (Job Family) + Research Associate (Job Class), 
        Faculty (Job Type) + Instructor (Job Family)), 
        ASSIGNMENT_STATUS (is not Terminated before July 2016), 
        EMPLOYEE_FULL_PART_TIME_DESCR (is Fulltime-Regular, Fulltime-Temporary, or Parttime-Regular), 
        EMERITUS_STATUS (is NO or YES), 
        DEPARTMENT_DESCR (is Pharmacy), 
        RESPONSIBILITY_CENTER_DESCR (is Dental Medicine, GSPH, Medicine, Nursing, Pharmacy, SHRS, SVC Health Sciences, or UPMC Hillman Cancer Center)
    */
    (urc.RESPONSIBILITY_CENTER_CD = 33
    and udj.JOB_TYPE = 'Faculty'
    and udj.JOB_FAMILY not in ('Lecturer', 'Scholar', 'Instructor')
    and not (udj.JOB_FAMILY = 'Professor' and udj.JOB_CLASS in (
        'Adjunct Assistant', 'Adjunct', 'Adjunct Associate', 'Research Assistant', 'Clinical Assistant',
        'Clinical Associate', 'Distinguished Service', 'Research Associate'))
    and (uas.ASSIGNMENT_STATUS_KEY not in (17, 18) or
        (uas.ASSIGNMENT_STATUS_KEY in (17, 18) and ude.last_day_worked_dt >= '01-JUL-16'))
    and efpt.EMPLOYEE_FULL_PART_TIME_DESCR in ('Fulltime-Regular', 'Fulltime-Temporary', 'Parttime-Regular')
    )
    or
    -- SHRS
    /*Excluded (13) 
    Faculty (JOB_TYPE) + Lecturer (JOB_FAMILY), 
    Faculty (JOB_TYPE) + Instructor (JOB_FAMILY) + Adjunct Assistant (JOB_CLASS), 
    Faculty (JOB_TYPE) + Instructor (JOB_FAMILY) + Adjunct Clinical (JOB_CLASS),
    Faculty (JOB_TYPE) + Instructor (JOB_FAMILY), 
    Faculty (JOB_TYPE) + Professor (JOB_FAMILY) + Adjunct Assistant (JOB_CLASS), 
    Faculty (JOB_TYPE) + Professor (JOB_FAMILY) + Adjunct (JOB_CLASS), 
    Faculty (JOB_TYPE) + Professor (JOB_FAMILY) + Research Assistant (JOB_CLASS), 
    Faculty (JOB_TYPE) + Professor (JOB_FAMILY) + Adjunct Clinical Assistant (JOB_CLASS), 
    Faculty (JOB_TYPE) + Professor (JOB_FAMILY) + Adjunct Research Assistant (JOB_CLASS), 
    Faculty (JOB_TYPE) + Professor (JOB_FAMILY) + Adjunct Associate (JOB_CLASS), 
    Faculty (JOB_TYPE) + Professor (JOB_FAMILY) + Clinical Assistant (JOB_CLASS), 
    Faculty (JOB_TYPE) + Professor (JOB_FAMILY) + Distinguished (JOB_CLASS), 
    Faculty (JOB_TYPE) + Professor (JOB_FAMILY) + Visiting (JOB_CLASS)
    ASSIGNMENT_STATUS is Active, Terminated between July 2016 and June 2019, Terminated between July 2019 and June 2020, or Terminated since July 2020
    EMPLOYEE_FULL_PART_TIME_DESCR is Parttime-Temporary, Parttime-Regular, or Fulltime-Regular
    DEPARTMENT_DESCR is SHRS
    RESPONSIBILITY_CENTER_DESCR is Dental Medicine, GSPH, Medicine, Nursing, Pharmacy, SHRS, SVC Health Sciences, or UPMC Hillman Cancer Center"

    */
    (urc.RESPONSIBILITY_CENTER_CD = 39
    and udj.JOB_TYPE in ('Academic', 'Faculty', 'Post Doctoral')
    and not (udj.JOB_TYPE = 'Faculty' and udj.JOB_FAMILY in ('Lecturer', 'Instructor'))
    and not (udj.JOB_TYPE = 'Faculty' and udj.JOB_FAMILY = 'Professor' and udj.JOB_CLASS in (
        'Adjunct Assistant', 'Adjunct', 'Research Assistant', 'Adjunct Clinical Assistant', 'Adjunct Research Assistant',
        'Adjunct Associate', 'Clinical Assistant', 'Distinguished', 'Visiting'))
    and (uas.ASSIGNMENT_STATUS_KEY not in (17, 18) or
        (uas.ASSIGNMENT_STATUS_KEY in (17, 18) and ude.last_day_worked_dt >= '01-JUL-16'))
    and efpt.EMPLOYEE_FULL_PART_TIME_DESCR in ('Parttime-Temporary', 'Parttime-Regular', 'Fulltime-Regular')
    )
    or
    -- Katz
    (urc.RESPONSIBILITY_CENTER_CD = 21
    and udj.JOB_TYPE in ('Academic', 'Faculty', 'Post Doctoral')
    and uas.ASSIGNMENT_STATUS_KEY not in (4, 17, 18, 20, 35)
    and efpt.EMPLOYEE_FULL_PART_TIME_DESCR in ('Fulltime-Regular', 'Fulltime-Temporary')
    )
    or
    -- Medicine
    (urc.RESPONSIBILITY_CENTER_CD = 35
    and udj.JOB_TYPE in ('Faculty', 'Post Doctoral')
    and not (udj.JOB_TYPE = 'Faculty' and udj.JOB_FAMILY in ('Lecturer', 'Scholar', 'Instructor'))
    and not (udj.JOB_TYPE = 'Faculty' and udj.JOB_FAMILY = 'Professor' and udj.JOB_CLASS in 
        ('Adjunct Assistant', 'Adjunct', 'Adjunct Associate', 'Research Assistant', 
         'Clinical Assistant', 'Clinical Associate', 'Distinguished Service', 'Research Associate'))
    and efpt.EMPLOYEE_FULL_PART_TIME_DESCR in ('Fulltime-Regular', 'Fulltime-Temporary')
    and (uas.ASSIGNMENT_STATUS_KEY not in (4, 17, 18, 20, 35) or 
        (uas.ASSIGNMENT_STATUS_KEY in (17, 18) and ude.last_day_worked_dt >= '01-JUL-16'))
    )
    or
    -- SCI
    (urc.RESPONSIBILITY_CENTER_CD = 94
    and udj.JOB_TYPE = 'Faculty'
    and efpt.EMPLOYEE_FULL_PART_TIME_DESCR = 'Fulltime-Regular'
    and uas.ASSIGNMENT_STATUS_KEY not in (4, 17, 18, 20, 35)
    )
    or
    -- Social Work
    (urc.RESPONSIBILITY_CENTER_CD = 26
    and udj.JOB_TYPE in ('Acedemic', 'Faculty', 'Post Doctoral')
    and uas.ASSIGNMENT_STATUS_KEY not in (4, 17, 18, 20, 35)
    and (ude.FACULTY_EMERITUS_FLG is null or ude.FACULTY_EMERITUS_FLG = 'NO')
    and efpt.EMPLOYEE_FULL_PART_TIME_DESCR = 'Fulltime-Regular'
    and ude.FACULTY_TENURE_STATUS_DESCR in ('Tenure Stream', 'Tenured')
    )
)
