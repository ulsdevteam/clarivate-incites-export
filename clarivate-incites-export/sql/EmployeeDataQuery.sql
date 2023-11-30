select EMPLID,
       EMPLOYEE_NBR,
       USERNAME,
       LAST_NAME,
       FIRST_NAME,
       EMAIL_ADDRESS,
       DEPARTMENT_CD,
       DEPARTMENT_DESCR,
       RESPONSIBILITY_CENTER_CD,
       RESPONSIBILITY_CENTER_DESCR,
       FACULTY_TENURE_STATUS_DESCR,
       JOB_KEY,
       JOB_TYPE,
       JOB_FAMILY,
       JOB_CLASS,
       BUILDING_NAME,
       ROOM_NBR

from (select emplid,
             employee_key,
             employee_nbr,
             username,
             last_name,
             first_name,
             email_address,
             last_day_worked_dt,
             job_key,
             job_type,
             job_family,
             job_class,
             building_name,
             room_nbr,
             EMPLOYEE_FULL_PART_TIME_DESCR,
             assignment_status_descr,
             assignment_status_key,
             department_cd,
             department_descr,
             department_subdivision_descr,
             responsibility_center_cd,
             responsibility_center_descr,
             faculty_emeritus_flg,
             faculty_tenure_status_descr,
             rank() OVER (PARTITION BY emplid
	   ORDER BY full_dt DESC) ranker
      from (select ude.emplid,
                   cal.full_dt,
                   pye.employee_key,
                   ude.employee_nbr,
                   ude.username,
                   ude.last_name,
                   ude.first_name,
                   ude.email_address,
                   udj.job_key,
                   udj.job_type,
                   udj.job_family,
                   udj.job_class,
                   ude.building_name,
                   ude.room_nbr,
                   efpt.EMPLOYEE_FULL_PART_TIME_DESCR,
                   uas.assignment_status_descr,
                   uas.assignment_status_key,
                   udd.department_cd,
                   udd.department_descr,
                   udd.department_subdivision_descr,
                   udd.responsibility_center_cd,
                   udd.responsibility_center_descr,
                   ude.faculty_emeritus_flg,
                   ude.faculty_tenure_status_descr,
                   ude.last_day_worked_dt

            from UD_DATA.PY_EMPLOYMENT pye
                     join UD_DATA.UD_EMPLOYEE ude on pye.EMPLOYEE_KEY = ude.employee_key
                     join UD_DATA.UD_EMPLOYEE_FULL_PART_TIME efpt
                          on pye.employee_full_part_time_key = efpt.employee_full_part_time_key
                     join UD_DATA.UD_JOB udj on pye.job_key = udj.job_key
                     join UD_DATA.UD_DEPARTMENT udd on pye.department_key = udd.department_key
                     join UD_DATA.UD_CALENDAR cal on pye.calendar_key = cal.calendar_key
                     join UD_DATA.UD_ASSIGNMENT_STATUS uas on pye.assignment_status_key = uas.assignment_status_key
            where udj.current_flg = 1
              and udd.current_flg = 1
              and udj.job_type in ('Academic', 'Faculty', 'Post Doctoral')
              and cal.calendar_key = SYS_CONTEXT('G$CONTEXT', 'PYM_CU_CAL_K_0000')

            UNION

            select ude.emplid,
                   m.last_dt as full_dt,
                   pye.employee_key,
                   ude.employee_nbr,
                   ude.username,
                   ude.last_name,
                   ude.first_name,
                   ude.email_address,
                   udj.job_key,
                   udj.job_type,
                   udj.job_family,
                   udj.job_class,
                   ude.building_name,
                   ude.room_nbr,
                   efpt.EMPLOYEE_FULL_PART_TIME_DESCR,
                   uas.assignment_status_descr,
                   uas.assignment_status_key,
                   udd.department_cd,
                   udd.department_descr,
                   udd.department_subdivision_descr,
                   udd.responsibility_center_cd,
                   udd.responsibility_center_descr,
                   ude.faculty_emeritus_flg,
                   ude.faculty_tenure_status_descr,
                   ude.last_day_worked_dt

            from UD_DATA.PY_EMPLOYMENT pye
                     join UD_DATA.UD_EMPLOYEE ude on pye.EMPLOYEE_KEY = ude.employee_key
                     join UD_DATA.UD_EMPLOYEE_FULL_PART_TIME efpt
                          on pye.employee_full_part_time_key = efpt.employee_full_part_time_key
                     join UD_DATA.UD_JOB udj on pye.job_key = udj.job_key
                     join UD_DATA.UD_DEPARTMENT udd on pye.department_key = udd.department_key
                     join UD_DATA.UD_CALENDAR cal on pye.calendar_key = cal.calendar_key
                     join UD_DATA.UD_ASSIGNMENT_STATUS uas on pye.assignment_status_key = uas.assignment_status_key
                     inner join (select ude.emplid, max(cal.full_dt) as last_dt
                                 from UD_DATA.PY_EMPLOYMENT pye
                                          join UD_DATA.UD_EMPLOYEE ude on pye.EMPLOYEE_KEY = ude.employee_key
                                          join UD_DATA.UD_CALENDAR cal on pye.calendar_key = cal.calendar_key
                                 where cal.full_dt = '31-DEC-19'
                                 group by ude.emplid) m
                                on m.emplid = ude.emplid and m.last_dt = cal.full_dt

            where udj.current_flg = 1
              and udd.current_flg = 1
              and udj.job_type in ('Academic', 'Faculty', 'Post Doctoral')) u) x
where ranker = 1
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
        (RESPONSIBILITY_CENTER_CD = 23
            and JOB_TYPE = 'Faculty'
            and (ASSIGNMENT_STATUS_KEY not in (17, 18) or
                 (ASSIGNMENT_STATUS_KEY in (17, 18) and last_day_worked_dt >= TO_DATE(CONCAT('01 JUL ', EXTRACT(YEAR FROM ADD_MONTHS(CURRENT_DATE, -6)) - 3), 'DD MON YYYY')))
            and JOB_FAMILY not in
                ('Adjunct Assistant', 'Adjunct Associate', 'Visiting Research', 'Adjunct Research',
                 'Adjunct Research Assistant',
                 'Adjunct', 'Lecturer', 'Scholar', 'Instructor'))
        or
        -- Dental Medicine
        RESPONSIBILITY_CENTER_CD = 31
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
        (RESPONSIBILITY_CENTER_CD = 33
            and JOB_TYPE = 'Faculty'
            and JOB_FAMILY not in ('Lecturer', 'Scholar', 'Instructor')
            and not (JOB_FAMILY = 'Professor' and JOB_CLASS in (
                                                                'Adjunct Assistant', 'Adjunct', 'Adjunct Associate',
                                                                'Research Assistant', 'Clinical Assistant',
                                                                'Clinical Associate', 'Distinguished Service',
                                                                'Research Associate'))
            and (ASSIGNMENT_STATUS_KEY not in (17, 18) or
                 (ASSIGNMENT_STATUS_KEY in (17, 18) and last_day_worked_dt >= TO_DATE(CONCAT('01 JUL ', EXTRACT(YEAR FROM ADD_MONTHS(CURRENT_DATE, -6)) - 6), 'DD MON YYYY')))
            and EMPLOYEE_FULL_PART_TIME_DESCR in ('Fulltime-Regular', 'Fulltime-Temporary', 'Parttime-Regular')
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
        (RESPONSIBILITY_CENTER_CD = 39
            and not (JOB_TYPE = 'Faculty' and JOB_FAMILY in ('Lecturer', 'Instructor'))
            and not (JOB_TYPE = 'Faculty' and JOB_FAMILY = 'Professor' and JOB_CLASS in (
                                                                                         'Adjunct Assistant', 'Adjunct',
                                                                                         'Research Assistant',
                                                                                         'Adjunct Clinical Assistant',
                                                                                         'Adjunct Research Assistant',
                                                                                         'Adjunct Associate',
                                                                                         'Clinical Assistant',
                                                                                         'Distinguished', 'Visiting'))
            and (ASSIGNMENT_STATUS_KEY not in (17, 18) or
                 (ASSIGNMENT_STATUS_KEY in (17, 18) and last_day_worked_dt >= TO_DATE(CONCAT('01 JUL ', EXTRACT(YEAR FROM ADD_MONTHS(CURRENT_DATE, -6)) - 6), 'DD MON YYYY')))
            and EMPLOYEE_FULL_PART_TIME_DESCR in ('Parttime-Temporary', 'Parttime-Regular', 'Fulltime-Regular')
            )
        or
        -- Katz
        (RESPONSIBILITY_CENTER_CD = 21
            and ASSIGNMENT_STATUS_KEY not in (4, 17, 18, 20, 35)
            and EMPLOYEE_FULL_PART_TIME_DESCR in ('Fulltime-Regular', 'Fulltime-Temporary')
            )
        or
        -- Medicine
        (RESPONSIBILITY_CENTER_CD = 35
            and JOB_TYPE = 'Faculty'
            and not (JOB_FAMILY = 'Scholar')
            and not (JOB_FAMILY = 'Professor' and JOB_CLASS in
                ('Visiting', 'Visiting Associate', 'Visiting Assistant',
                'Visiting Research Assistant', 'Visiting Research Associate'))
            and not (JOB_FAMILY = 'Instructor' and JOB_CLASS in 
                ('Visiting', 'Visiting Research'))
            and EMPLOYEE_FULL_PART_TIME_DESCR in ('Fulltime-Regular', 'Parttime-Regular')
            and (ASSIGNMENT_STATUS_KEY not in (4, 17, 18, 20, 35) or
                 (ASSIGNMENT_STATUS_KEY in (17, 18) and last_day_worked_dt >= TO_DATE(CONCAT('01 JUL ', EXTRACT(YEAR FROM ADD_MONTHS(CURRENT_DATE, -6)) - 3), 'DD MON YYYY')))
            )
        or
        -- SCI
        (RESPONSIBILITY_CENTER_CD = 94
            and JOB_TYPE = 'Faculty'
            and EMPLOYEE_FULL_PART_TIME_DESCR = 'Fulltime-Regular'
            and ASSIGNMENT_STATUS_KEY not in (4, 17, 18, 20, 35)
            )
        or
        -- Social Work
        (RESPONSIBILITY_CENTER_CD = 26
            and ASSIGNMENT_STATUS_KEY not in (4, 17, 18, 20, 35)
            and (FACULTY_EMERITUS_FLG is null or FACULTY_EMERITUS_FLG = 'NO')
            and EMPLOYEE_FULL_PART_TIME_DESCR = 'Fulltime-Regular'
            and FACULTY_TENURE_STATUS_DESCR in ('Tenure Stream', 'Tenured')
            )
        or
        -- Nursing
        (RESPONSIBILITY_CENTER_CD = 32
            and JOB_TYPE = 'Faculty'
            and JOB_FAMILY not in (
                                   'Lecturer', 'Scholar', 'Adjunct Assistant', 'Adjunct', 'Adjunct Associate',
                                   'Research Assistant',
                                   'Clinical Assistant', 'Clinical Associate', 'Distinguished Service',
                                   'Research Associate', 'Instructor')
            and EMPLOYEE_FULL_PART_TIME_DESCR in
                ('Fulltime-Regular', 'Fulltime-Temporary', 'Parttime-Temporary', 'Parttime-Regular')
            and ASSIGNMENT_STATUS_KEY not in (4, 17, 18, 20, 35))
        or
        -- GSPH
        (RESPONSIBILITY_CENTER_CD = 34 and ASSIGNMENT_STATUS_KEY not in (4, 17, 18, 20, 35))
        or
        /* No response, default criteria:
        Chancellor
        Kenneth P. Dietrich School of Arts & Sciences
        Education
        Law
        GSPIA
        SVC Health Sciences
        UCIS
        LRDC
        UCSUR
        SVC Research
        SVC and Provost
        UPMC Hillman Cancer Center
        */
        (RESPONSIBILITY_CENTER_CD in ('01', '03', '06', '10', '22', '24', '25', '30', '51', '55', '78', '81')
            and JOB_TYPE = 'Faculty'
            and ASSIGNMENT_STATUS_KEY not in (4, 17, 18, 20, 35)
            and EMPLOYEE_FULL_PART_TIME_DESCR = 'Fulltime-Regular'
            )
    )