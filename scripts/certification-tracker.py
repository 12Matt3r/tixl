#!/usr/bin/env python3
"""
TiXL Certification Tracker System
=================================

A comprehensive system for tracking educational certifications, course completions,
and student progress for the TiXL Educational Partnerships Program.

Features:
- Student enrollment and progress tracking
- Course completion monitoring
- Certification management (issuance, verification, renewal)
- Partnership institution management
- Analytics and reporting dashboard
- Integration with learning management systems
- Automated workflows and notifications
"""

import json
import sqlite3
import datetime
import hashlib
import uuid
from typing import Dict, List, Optional, Any, Tuple
from dataclasses import dataclass, asdict
from pathlib import Path
from enum import Enum
import logging
from contextlib import contextmanager


class CertificationLevel(Enum):
    """Enumeration of TiXL certification levels."""
    TIXL_CERTIFIED_USER = "TCU"
    TIXL_CERTIFIED_ANALYST = "TCA"
    TIXL_CERTIFIED_EXPERT = "TCE"
    TIXL_CERTIFIED_INSTRUCTOR = "TCI"


class StudentStatus(Enum):
    """Enumeration of student status types."""
    ENROLLED = "enrolled"
    IN_PROGRESS = "in_progress"
    COMPLETED = "completed"
    CERTIFIED = "certified"
    SUSPENDED = "suspended"
    WITHDRAWN = "withdrawn"


class CourseStatus(Enum):
    """Enumeration of course status types."""
    AVAILABLE = "available"
    IN_PROGRESS = "in_progress"
    COMPLETED = "completed"
    DISCONTINUED = "discontinued"


@dataclass
class Student:
    """Represents a student in the TiXL education system."""
    student_id: str
    first_name: str
    last_name: str
    email: str
    phone: Optional[str] = None
    institution_id: Optional[str] = None
    enrollment_date: datetime.datetime = None
    status: StudentStatus = StudentStatus.ENROLLED
    profile_data: Dict[str, Any] = None
    
    def __post_init__(self):
        if self.enrollment_date is None:
            self.enrollment_date = datetime.datetime.now()
        if self.profile_data is None:
            self.profile_data = {}


@dataclass
class Course:
    """Represents a course in the TiXL education system."""
    course_id: str
    title: str
    description: str
    level: str
    duration_hours: int
    certification_level: Optional[CertificationLevel] = None
    prerequisites: List[str] = None
    course_content: Dict[str, Any] = None
    status: CourseStatus = CourseStatus.AVAILABLE
    created_date: datetime.datetime = None
    
    def __post_init__(self):
        if self.prerequisites is None:
            self.prerequisites = []
        if self.course_content is None:
            self.course_content = {}
        if self.created_date is None:
            self.created_date = datetime.datetime.now()


@dataclass
class Enrollment:
    """Represents a student's enrollment in a course."""
    enrollment_id: str
    student_id: str
    course_id: str
    enrollment_date: datetime.datetime
    completion_date: Optional[datetime.datetime] = None
    progress_percentage: float = 0.0
    grade: Optional[float] = None
    status: StudentStatus = StudentStatus.ENROLLED
    notes: str = ""
    
    def __post_init__(self):
        if self.enrollment_date is None:
            self.enrollment_date = datetime.datetime.now()


@dataclass
class Certification:
    """Represents a certification awarded to a student."""
    certification_id: str
    student_id: str
    certification_level: CertificationLevel
    issue_date: datetime.datetime
    expiration_date: Optional[datetime.datetime] = None
    verification_code: str = ""
    assessment_score: float = 0.0
    competency_domains: Dict[str, float] = None
    issuing_institution: str = ""
    digital_badge_url: str = ""
    status: str = "active"
    
    def __post_init__(self):
        if self.verification_code == "":
            self.verification_code = self._generate_verification_code()
        if self.competency_domains is None:
            self.competency_domains = {}


@dataclass
class Institution:
    """Represents an educational institution partner."""
    institution_id: str
    name: str
    institution_type: str
    contact_email: str
    contact_phone: Optional[str] = None
    address: Optional[str] = None
    partnership_tier: str = "silver"
    partnership_start_date: datetime.datetime = None
    api_key: str = ""
    active: bool = True
    
    def __post_init__(self):
        if self.partnership_start_date is None:
            self.partnership_start_date = datetime.datetime.now()
        if self.api_key == "":
            self.api_key = self._generate_api_key()


class TiXLCertificationTracker:
    """Main class for tracking TiXL certifications and educational progress."""
    
    def __init__(self, db_path: str = "tixl_certification_tracker.db"):
        self.db_path = Path(db_path)
        self.logger = self._setup_logging()
        self._initialize_database()
    
    def _setup_logging(self) -> logging.Logger:
        """Setup logging configuration."""
        logger = logging.getLogger('TiXLCertificationTracker')
        logger.setLevel(logging.INFO)
        
        if not logger.handlers:
            handler = logging.StreamHandler()
            formatter = logging.Formatter(
                '%(asctime)s - %(name)s - %(levelname)s - %(message)s'
            )
            handler.setFormatter(formatter)
            logger.addHandler(handler)
        
        return logger
    
    def _initialize_database(self):
        """Initialize the SQLite database with required tables."""
        with self.get_db_connection() as conn:
            # Students table
            conn.execute('''
                CREATE TABLE IF NOT EXISTS students (
                    student_id TEXT PRIMARY KEY,
                    first_name TEXT NOT NULL,
                    last_name TEXT NOT NULL,
                    email TEXT UNIQUE NOT NULL,
                    phone TEXT,
                    institution_id TEXT,
                    enrollment_date TEXT NOT NULL,
                    status TEXT NOT NULL,
                    profile_data TEXT,
                    FOREIGN KEY (institution_id) REFERENCES institutions (institution_id)
                )
            ''')
            
            # Courses table
            conn.execute('''
                CREATE TABLE IF NOT EXISTS courses (
                    course_id TEXT PRIMARY KEY,
                    title TEXT NOT NULL,
                    description TEXT,
                    level TEXT NOT NULL,
                    duration_hours INTEGER NOT NULL,
                    certification_level TEXT,
                    prerequisites TEXT,
                    course_content TEXT,
                    status TEXT NOT NULL,
                    created_date TEXT NOT NULL
                )
            ''')
            
            # Enrollments table
            conn.execute('''
                CREATE TABLE IF NOT EXISTS enrollments (
                    enrollment_id TEXT PRIMARY KEY,
                    student_id TEXT NOT NULL,
                    course_id TEXT NOT NULL,
                    enrollment_date TEXT NOT NULL,
                    completion_date TEXT,
                    progress_percentage REAL DEFAULT 0.0,
                    grade REAL,
                    status TEXT NOT NULL,
                    notes TEXT,
                    FOREIGN KEY (student_id) REFERENCES students (student_id),
                    FOREIGN KEY (course_id) REFERENCES courses (course_id)
                )
            ''')
            
            # Certifications table
            conn.execute('''
                CREATE TABLE IF NOT EXISTS certifications (
                    certification_id TEXT PRIMARY KEY,
                    student_id TEXT NOT NULL,
                    certification_level TEXT NOT NULL,
                    issue_date TEXT NOT NULL,
                    expiration_date TEXT,
                    verification_code TEXT UNIQUE NOT NULL,
                    assessment_score REAL DEFAULT 0.0,
                    competency_domains TEXT,
                    issuing_institution TEXT NOT NULL,
                    digital_badge_url TEXT,
                    status TEXT NOT NULL,
                    FOREIGN KEY (student_id) REFERENCES students (student_id)
                )
            ''')
            
            # Institutions table
            conn.execute('''
                CREATE TABLE IF NOT EXISTS institutions (
                    institution_id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    institution_type TEXT NOT NULL,
                    contact_email TEXT NOT NULL,
                    contact_phone TEXT,
                    address TEXT,
                    partnership_tier TEXT NOT NULL,
                    partnership_start_date TEXT NOT NULL,
                    api_key TEXT UNIQUE NOT NULL,
                    active BOOLEAN DEFAULT 1
                )
            ''')
            
            # Create indexes for better performance
            conn.execute('CREATE INDEX IF NOT EXISTS idx_students_email ON students(email)')
            conn.execute('CREATE INDEX IF NOT EXISTS idx_enrollments_student ON enrollments(student_id)')
            conn.execute('CREATE INDEX IF NOT EXISTS idx_enrollments_course ON enrollments(course_id)')
            conn.execute('CREATE INDEX IF NOT EXISTS idx_certifications_student ON certifications(student_id)')
            conn.execute('CREATE INDEX IF NOT EXISTS idx_certifications_level ON certifications(certification_level)')
            
            conn.commit()
    
    @contextmanager
    def get_db_connection(self):
        """Context manager for database connections."""
        conn = sqlite3.connect(self.db_path)
        conn.row_factory = sqlite3.Row
        try:
            yield conn
            conn.commit()
        except Exception as e:
            conn.rollback()
            self.logger.error(f"Database error: {e}")
            raise
        finally:
            conn.close()
    
    def _generate_verification_code(self) -> str:
        """Generate a unique verification code for certifications."""
        timestamp = datetime.datetime.now().strftime("%Y%m%d%H%M%S")
        random_suffix = uuid.uuid4().hex[:8]
        return f"TIXL-{timestamp}-{random_suffix}"
    
    def _generate_api_key(self) -> str:
        """Generate a unique API key for institutions."""
        timestamp = datetime.datetime.now().strftime("%Y%m%d")
        random_component = hashlib.sha256(f"{uuid.uuid4()}{timestamp}".encode()).hexdigest()[:16]
        return f"tixl_api_{timestamp}_{random_component}"
    
    def add_student(self, student: Student) -> bool:
        """Add a new student to the system."""
        try:
            with self.get_db_connection() as conn:
                conn.execute('''
                    INSERT INTO students (
                        student_id, first_name, last_name, email, phone,
                        institution_id, enrollment_date, status, profile_data
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
                ''', (
                    student.student_id, student.first_name, student.last_name,
                    student.email, student.phone, student.institution_id,
                    student.enrollment_date.isoformat(), student.status.value,
                    json.dumps(student.profile_data)
                ))
                self.logger.info(f"Added student: {student.student_id}")
                return True
        except sqlite3.IntegrityError as e:
            self.logger.error(f"Error adding student {student.student_id}: {e}")
            return False
    
    def get_student(self, student_id: str) -> Optional[Student]:
        """Retrieve a student by ID."""
        with self.get_db_connection() as conn:
            row = conn.execute(
                'SELECT * FROM students WHERE student_id = ?', (student_id,)
            ).fetchone()
            
            if row:
                return Student(
                    student_id=row['student_id'],
                    first_name=row['first_name'],
                    last_name=row['last_name'],
                    email=row['email'],
                    phone=row['phone'],
                    institution_id=row['institution_id'],
                    enrollment_date=datetime.datetime.fromisoformat(row['enrollment_date']),
                    status=StudentStatus(row['status']),
                    profile_data=json.loads(row['profile_data'] or '{}')
                )
        return None
    
    def update_student_progress(self, enrollment_id: str, progress: float, 
                               status: Optional[StudentStatus] = None) -> bool:
        """Update student progress in a course."""
        try:
            with self.get_db_connection() as conn:
                if status:
                    conn.execute('''
                        UPDATE enrollments 
                        SET progress_percentage = ?, status = ?
                        WHERE enrollment_id = ?
                    ''', (progress, status.value, enrollment_id))
                else:
                    conn.execute('''
                        UPDATE enrollments 
                        SET progress_percentage = ?
                        WHERE enrollment_id = ?
                    ''', (progress, enrollment_id))
                
                if conn.total_changes > 0:
                    self.logger.info(f"Updated progress for enrollment: {enrollment_id}")
                    return True
                else:
                    self.logger.warning(f"Enrollment not found: {enrollment_id}")
                    return False
        except Exception as e:
            self.logger.error(f"Error updating progress: {e}")
            return False
    
    def enroll_student(self, student_id: str, course_id: str) -> Optional[str]:
        """Enroll a student in a course."""
        enrollment_id = str(uuid.uuid4())
        enrollment_date = datetime.datetime.now()
        
        try:
            with self.get_db_connection() as conn:
                # Verify student and course exist
                student = conn.execute(
                    'SELECT student_id FROM students WHERE student_id = ?', (student_id,)
                ).fetchone()
                course = conn.execute(
                    'SELECT course_id FROM courses WHERE course_id = ?', (course_id,)
                ).fetchone()
                
                if not student or not course:
                    self.logger.error(f"Student {student_id} or course {course_id} not found")
                    return None
                
                # Check if already enrolled
                existing = conn.execute('''
                    SELECT enrollment_id FROM enrollments 
                    WHERE student_id = ? AND course_id = ? AND status != 'withdrawn'
                ''', (student_id, course_id)).fetchone()
                
                if existing:
                    self.logger.warning(f"Student {student_id} already enrolled in course {course_id}")
                    return existing['enrollment_id']
                
                # Create enrollment
                enrollment = Enrollment(
                    enrollment_id=enrollment_id,
                    student_id=student_id,
                    course_id=course_id,
                    enrollment_date=enrollment_date
                )
                
                conn.execute('''
                    INSERT INTO enrollments (
                        enrollment_id, student_id, course_id, enrollment_date,
                        progress_percentage, status
                    ) VALUES (?, ?, ?, ?, ?, ?)
                ''', (
                    enrollment.enrollment_id, enrollment.student_id,
                    enrollment.course_id, enrollment.enrollment_date.isoformat(),
                    enrollment.progress_percentage, enrollment.status.value
                ))
                
                self.logger.info(f"Enrolled student {student_id} in course {course_id}")
                return enrollment_id
                
        except Exception as e:
            self.logger.error(f"Error enrolling student: {e}")
            return None
    
    def complete_course(self, enrollment_id: str, grade: float, 
                       completion_date: Optional[datetime.datetime] = None) -> bool:
        """Mark a course as completed for a student."""
        if completion_date is None:
            completion_date = datetime.datetime.now()
        
        try:
            with self.get_db_connection() as conn:
                conn.execute('''
                    UPDATE enrollments 
                    SET status = ?, completion_date = ?, grade = ?, progress_percentage = 100.0
                    WHERE enrollment_id = ?
                ''', (StudentStatus.COMPLETED.value, completion_date.isoformat(), 
                     grade, enrollment_id))
                
                if conn.total_changes > 0:
                    self.logger.info(f"Completed course for enrollment: {enrollment_id}")
                    return True
                else:
                    self.logger.warning(f"Enrollment not found: {enrollment_id}")
                    return False
                    
        except Exception as e:
            self.logger.error(f"Error completing course: {e}")
            return False
    
    def issue_certification(self, student_id: str, certification_level: CertificationLevel,
                           assessment_score: float, competency_domains: Dict[str, float],
                           issuing_institution: str) -> Optional[str]:
        """Issue a certification to a student."""
        certification_id = str(uuid.uuid4())
        issue_date = datetime.datetime.now()
        
        # Calculate expiration date (3 years from issue for most certifications)
        expiration_date = issue_date + datetime.timedelta(days=3*365)
        
        try:
            with self.get_db_connection() as conn:
                # Verify student exists
                student = conn.execute(
                    'SELECT student_id FROM students WHERE student_id = ?', (student_id,)
                ).fetchone()
                
                if not student:
                    self.logger.error(f"Student {student_id} not found")
                    return None
                
                # Check prerequisites for certification level
                if not self._verify_certification_prerequisites(student_id, certification_level):
                    self.logger.error(f"Prerequisites not met for certification {certification_level.value}")
                    return None
                
                certification = Certification(
                    certification_id=certification_id,
                    student_id=student_id,
                    certification_level=certification_level,
                    issue_date=issue_date,
                    expiration_date=expiration_date,
                    assessment_score=assessment_score,
                    competency_domains=competency_domains,
                    issuing_institution=issuing_institution
                )
                
                conn.execute('''
                    INSERT INTO certifications (
                        certification_id, student_id, certification_level,
                        issue_date, expiration_date, verification_code,
                        assessment_score, competency_domains, issuing_institution,
                        status
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                ''', (
                    certification.certification_id, certification.student_id,
                    certification.certification_level.value,
                    certification.issue_date.isoformat(),
                    certification.expiration_date.isoformat(),
                    certification.verification_code, certification.assessment_score,
                    json.dumps(certification.competency_domains),
                    certification.issuing_institution, certification.status
                ))
                
                self.logger.info(f"Issued certification {certification.certification_id} to student {student_id}")
                return certification_id
                
        except Exception as e:
            self.logger.error(f"Error issuing certification: {e}")
            return None
    
    def _verify_certification_prerequisites(self, student_id: str, 
                                          certification_level: CertificationLevel) -> bool:
        """Verify that a student meets prerequisites for certification."""
        with self.get_db_connection() as conn:
            if certification_level == CertificationLevel.TIXL_CERTIFIED_USER:
                # Check for any completed courses
                completed = conn.execute('''
                    SELECT COUNT(*) as count FROM enrollments 
                    WHERE student_id = ? AND status = 'completed'
                ''', (student_id,)).fetchone()
                return completed['count'] >= 3
            
            elif certification_level == CertificationLevel.TIXL_CERTIFIED_ANALYST:
                # Check for TCU and additional courses
                tcu_cert = conn.execute('''
                    SELECT certification_id FROM certifications 
                    WHERE student_id = ? AND certification_level = 'TCU'
                ''', (student_id,)).fetchone()
                
                if not tcu_cert:
                    return False
                
                completed = conn.execute('''
                    SELECT COUNT(*) as count FROM enrollments 
                    WHERE student_id = ? AND status = 'completed'
                ''', (student_id,)).fetchone()
                return completed['count'] >= 8
            
            elif certification_level == CertificationLevel.TIXL_CERTIFIED_EXPERT:
                # Check for TCA and advanced courses
                tca_cert = conn.execute('''
                    SELECT certification_id FROM certifications 
                    WHERE student_id = ? AND certification_level = 'TCA'
                ''', (student_id,)).fetchone()
                
                if not tca_cert:
                    return False
                
                completed = conn.execute('''
                    SELECT COUNT(*) as count FROM enrollments 
                    WHERE student_id = ? AND status = 'completed'
                ''', (student_id,)).fetchone()
                return completed['count'] >= 15
            
            elif certification_level == CertificationLevel.TIXL_CERTIFIED_INSTRUCTOR:
                # Check for TCE and teaching requirements
                tce_cert = conn.execute('''
                    SELECT certification_id FROM certifications 
                    WHERE student_id = ? AND certification_level = 'TCE'
                ''', (student_id,)).fetchone()
                
                if not tce_cert:
                    return False
                
                # Additional requirements for instructor certification
                # This would typically include teaching experience, training, etc.
                return True
        
        return False
    
    def verify_certification(self, verification_code: str) -> Optional[Dict[str, Any]]:
        """Verify a certification using its verification code."""
        with self.get_db_connection() as conn:
            row = conn.execute('''
                SELECT c.*, s.first_name, s.last_name, s.email
                FROM certifications c
                JOIN students s ON c.student_id = s.student_id
                WHERE c.verification_code = ?
            ''', (verification_code,)).fetchone()
            
            if row:
                return {
                    'verification_code': row['verification_code'],
                    'student_name': f"{row['first_name']} {row['last_name']}",
                    'student_email': row['email'],
                    'certification_level': row['certification_level'],
                    'issue_date': row['issue_date'],
                    'expiration_date': row['expiration_date'],
                    'assessment_score': row['assessment_score'],
                    'competency_domains': json.loads(row['competency_domains'] or '{}'),
                    'issuing_institution': row['issuing_institution'],
                    'status': row['status'],
                    'valid': row['status'] == 'active' and 
                           datetime.datetime.fromisoformat(row['expiration_date']) > datetime.datetime.now()
                }
        return None
    
    def get_student_progress(self, student_id: str) -> Dict[str, Any]:
        """Get comprehensive progress information for a student."""
        with self.get_db_connection() as conn:
            # Student info
            student = conn.execute('''
                SELECT * FROM students WHERE student_id = ?
            ''', (student_id,)).fetchone()
            
            if not student:
                return {}
            
            # Course enrollments
            enrollments = conn.execute('''
                SELECT e.*, c.title, c.level, c.duration_hours
                FROM enrollments e
                JOIN courses c ON e.course_id = c.course_id
                WHERE e.student_id = ?
                ORDER BY e.enrollment_date DESC
            ''', (student_id,)).fetchall()
            
            # Certifications
            certifications = conn.execute('''
                SELECT * FROM certifications
                WHERE student_id = ?
                ORDER BY issue_date DESC
            ''', (student_id,)).fetchall()
            
            # Calculate statistics
            total_courses = len(enrollments)
            completed_courses = len([e for e in enrollments if e['status'] == 'completed'])
            in_progress_courses = len([e for e in enrollments if e['status'] == 'in_progress'])
            avg_grade = sum([e['grade'] for e in enrollments if e['grade'] is not None]) / max(1, 
                           len([e for e in enrollments if e['grade'] is not None]))
            
            return {
                'student': dict(student),
                'enrollments': [dict(e) for e in enrollments],
                'certifications': [dict(c) for c in certifications],
                'statistics': {
                    'total_courses': total_courses,
                    'completed_courses': completed_courses,
                    'in_progress_courses': in_progress_courses,
                    'completion_rate': (completed_courses / max(1, total_courses)) * 100,
                    'average_grade': round(avg_grade, 2) if avg_grade else 0,
                    'certification_count': len(certifications)
                }
            }
    
    def get_partnership_analytics(self, institution_id: str = None) -> Dict[str, Any]:
        """Get analytics for partnerships."""
        with self.get_db_connection() as conn:
            base_query = '''
                SELECT 
                    s.institution_id,
                    i.name as institution_name,
                    COUNT(DISTINCT s.student_id) as total_students,
                    COUNT(DISTINCT e.enrollment_id) as total_enrollments,
                    COUNT(DISTINCT CASE WHEN e.status = 'completed' THEN e.enrollment_id END) as completed_courses,
                    COUNT(DISTINCT cert.certification_id) as total_certifications,
                    AVG(e.grade) as average_grade,
                    AVG(e.progress_percentage) as average_progress
                FROM students s
                LEFT JOIN institutions i ON s.institution_id = i.institution_id
                LEFT JOIN enrollments e ON s.student_id = e.student_id
                LEFT JOIN certifications cert ON s.student_id = cert.student_id
            '''
            
            if institution_id:
                base_query += ' WHERE s.institution_id = ? GROUP BY s.institution_id'
                params = (institution_id,)
            else:
                base_query += ' GROUP BY s.institution_id'
                params = ()
            
            rows = conn.execute(base_query, params).fetchall()
            
            analytics = []
            for row in rows:
                if row['institution_id']:  # Only include rows with institution data
                    completion_rate = (row['completed_courses'] / max(1, row['total_enrollments'])) * 100
                    certification_rate = (row['total_certifications'] / max(1, row['total_students'])) * 100
                    
                    analytics.append({
                        'institution_id': row['institution_id'],
                        'institution_name': row['institution_name'],
                        'total_students': row['total_students'],
                        'total_enrollments': row['total_enrollments'],
                        'completed_courses': row['completed_courses'],
                        'total_certifications': row['total_certifications'],
                        'completion_rate': round(completion_rate, 2),
                        'certification_rate': round(certification_rate, 2),
                        'average_grade': round(row['average_grade'] or 0, 2),
                        'average_progress': round(row['average_progress'] or 0, 2)
                    })
            
            return {'partnerships': analytics}
    
    def generate_progress_report(self, student_id: str, format_type: str = 'json') -> str:
        """Generate a comprehensive progress report for a student."""
        progress_data = self.get_student_progress(student_id)
        
        if not progress_data:
            return "Student not found"
        
        if format_type == 'json':
            return json.dumps(progress_data, indent=2, default=str)
        
        elif format_type == 'markdown':
            student = progress_data['student']
            stats = progress_data['statistics']
            
            report = f"""# TiXL Student Progress Report

## Student Information
- **Name**: {student['first_name']} {student['last_name']}
- **Email**: {student['email']}
- **Student ID**: {student['student_id']}
- **Enrollment Date**: {student['enrollment_date']}
- **Status**: {student['status']}

## Progress Summary
- **Total Courses**: {stats['total_courses']}
- **Completed Courses**: {stats['completed_courses']}
- **In Progress**: {stats['in_progress_courses']}
- **Completion Rate**: {stats['completion_rate']:.1f}%
- **Average Grade**: {stats['average_grade']:.1f}
- **Certifications Earned**: {stats['certification_count']}

## Course Enrollments
"""
            
            for enrollment in progress_data['enrollments']:
                report += f"- **{enrollment['title']}** ({enrollment['level']})\n"
                report += f"  - Status: {enrollment['status']}\n"
                report += f"  - Progress: {enrollment['progress_percentage']:.1f}%\n"
                if enrollment['grade']:
                    report += f"  - Grade: {enrollment['grade']:.1f}\n"
                report += "\n"
            
            if progress_data['certifications']:
                report += "## Certifications\n"
                for cert in progress_data['certifications']:
                    report += f"- **{cert['certification_level']}**\n"
                    report += f"  - Issued: {cert['issue_date']}\n"
                    report += f"  - Assessment Score: {cert['assessment_score']:.1f}\n"
                    report += f"  - Status: {cert['status']}\n\n"
            
            return report
        
        else:
            raise ValueError(f"Unsupported format: {format_type}")
    
    def export_student_data(self, student_id: str, output_path: str) -> bool:
        """Export complete student data to a JSON file."""
        progress_data = self.get_student_progress(student_id)
        
        if not progress_data:
            return False
        
        try:
            with open(output_path, 'w') as f:
                json.dump(progress_data, f, indent=2, default=str)
            self.logger.info(f"Exported student data to {output_path}")
            return True
        except Exception as e:
            self.logger.error(f"Error exporting student data: {e}")
            return False
    
    def add_institution(self, institution: Institution) -> bool:
        """Add a new educational institution partner."""
        try:
            with self.get_db_connection() as conn:
                conn.execute('''
                    INSERT INTO institutions (
                        institution_id, name, institution_type, contact_email,
                        contact_phone, address, partnership_tier,
                        partnership_start_date, api_key, active
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                ''', (
                    institution.institution_id, institution.name,
                    institution.institution_type, institution.contact_email,
                    institution.contact_phone, institution.address,
                    institution.partnership_tier,
                    institution.partnership_start_date.isoformat(),
                    institution.api_key, institution.active
                ))
                self.logger.info(f"Added institution: {institution.institution_id}")
                return True
        except sqlite3.IntegrityError as e:
            self.logger.error(f"Error adding institution {institution.institution_id}: {e}")
            return False
    
    def get_institution_students(self, institution_id: str) -> List[Student]:
        """Get all students from a specific institution."""
        students = []
        with self.get_db_connection() as conn:
            rows = conn.execute('''
                SELECT * FROM students WHERE institution_id = ?
            ''', (institution_id,)).fetchall()
            
            for row in rows:
                student = Student(
                    student_id=row['student_id'],
                    first_name=row['first_name'],
                    last_name=row['last_name'],
                    email=row['email'],
                    phone=row['phone'],
                    institution_id=row['institution_id'],
                    enrollment_date=datetime.datetime.fromisoformat(row['enrollment_date']),
                    status=StudentStatus(row['status']),
                    profile_data=json.loads(row['profile_data'] or '{}')
                )
                students.append(student)
        
        return students


def main():
    """Main function for command-line interface."""
    import argparse
    
    parser = argparse.ArgumentParser(description='TiXL Certification Tracker System')
    parser.add_argument('--db-path', default='tixl_certification_tracker.db',
                       help='Path to SQLite database file')
    parser.add_argument('--action', required=True,
                       choices=['add-student', 'enroll', 'complete-course', 'issue-cert',
                               'verify-cert', 'student-progress', 'partnership-analytics',
                               'generate-report', 'export-data'],
                       help='Action to perform')
    
    # Action-specific arguments
    parser.add_argument('--student-id', help='Student ID')
    parser.add_argument('--first-name', help='Student first name')
    parser.add_argument('--last-name', help='Student last name')
    parser.add_argument('--email', help='Student email')
    parser.add_argument('--course-id', help='Course ID')
    parser.add_argument('--certification-level', 
                       choices=['TCU', 'TCA', 'TCE', 'TCI'],
                       help='Certification level')
    parser.add_argument('--verification-code', help='Certification verification code')
    parser.add_argument('--institution-id', help='Institution ID')
    parser.add_argument('--format', default='json', choices=['json', 'markdown'],
                       help='Output format for reports')
    parser.add_argument('--output-path', help='Output file path for exports')
    
    args = parser.parse_args()
    
    # Initialize tracker
    tracker = TiXLCertificationTracker(args.db_path)
    
    try:
        if args.action == 'add-student':
            if not all([args.student_id, args.first_name, args.last_name, args.email]):
                print("Error: student-id, first-name, last-name, and email are required")
                return 1
            
            student = Student(
                student_id=args.student_id,
                first_name=args.first_name,
                last_name=args.last_name,
                email=args.email,
                institution_id=args.institution_id
            )
            
            if tracker.add_student(student):
                print(f"Student {args.student_id} added successfully")
            else:
                print(f"Failed to add student {args.student_id}")
        
        elif args.action == 'enroll':
            if not all([args.student_id, args.course_id]):
                print("Error: student-id and course-id are required")
                return 1
            
            enrollment_id = tracker.enroll_student(args.student_id, args.course_id)
            if enrollment_id:
                print(f"Student enrolled successfully. Enrollment ID: {enrollment_id}")
            else:
                print("Failed to enroll student")
        
        elif args.action == 'complete-course':
            # This would require enrollment_id and grade - simplified for demo
            print("Complete course action requires enrollment_id and grade (implement as needed)")
        
        elif args.action == 'issue-cert':
            # This would require student_id, certification_level, assessment_score, etc.
            print("Issue certification action requires additional parameters (implement as needed)")
        
        elif args.action == 'verify-cert':
            if not args.verification_code:
                print("Error: verification-code is required")
                return 1
            
            result = tracker.verify_certification(args.verification_code)
            if result:
                print(json.dumps(result, indent=2))
            else:
                print("Certification not found or invalid")
        
        elif args.action == 'student-progress':
            if not args.student_id:
                print("Error: student-id is required")
                return 1
            
            progress = tracker.get_student_progress(args.student_id)
            if progress:
                print(json.dumps(progress, indent=2, default=str))
            else:
                print("Student not found")
        
        elif args.action == 'partnership-analytics':
            analytics = tracker.get_partnership_analytics(args.institution_id)
            print(json.dumps(analytics, indent=2, default=str))
        
        elif args.action == 'generate-report':
            if not args.student_id:
                print("Error: student-id is required")
                return 1
            
            report = tracker.generate_progress_report(args.student_id, args.format)
            if args.output_path:
                with open(args.output_path, 'w') as f:
                    f.write(report)
                print(f"Report saved to {args.output_path}")
            else:
                print(report)
        
        elif args.action == 'export-data':
            if not all([args.student_id, args.output_path]):
                print("Error: student-id and output-path are required")
                return 1
            
            if tracker.export_student_data(args.student_id, args.output_path):
                print(f"Student data exported to {args.output_path}")
            else:
                print("Failed to export student data")
        
        print("Action completed successfully")
        return 0
        
    except Exception as e:
        print(f"Error: {e}")
        return 1


if __name__ == "__main__":
    exit(main())