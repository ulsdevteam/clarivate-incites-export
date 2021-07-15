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
    -- Health Sciences
    (urc.RESPONSIBILITY_CENTER_CD in (30, 31, 32, 33, 34, 35, 39, 55)
    and udj.JOB_TYPE = 'Faculty'
    and uas.ASSIGNMENT_STATUS_KEY = 28 
    and udd.DEPARTMENT_KEY != 36600)
)
