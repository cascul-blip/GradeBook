CREATE TABLE IF NOT EXISTS Students (
    Id       INTEGER PRIMARY KEY AUTOINCREMENT,
    Name     TEXT    NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS Classes (
    Id       INTEGER PRIMARY KEY AUTOINCREMENT,
    Name     TEXT    NOT NULL,
    IsActive INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS Enrollments (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    StudentId      INTEGER NOT NULL REFERENCES Students(Id) ON DELETE RESTRICT,
    ClassId        INTEGER NOT NULL REFERENCES Classes(Id)  ON DELETE RESTRICT,
    IsActive       INTEGER NOT NULL DEFAULT 1,
    EnrolledDate   TEXT    NOT NULL DEFAULT (datetime('now')),
    UnenrolledDate TEXT    NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS UX_Enrollments_ActivePair ON Enrollments(StudentId, ClassId) WHERE IsActive = 1;
CREATE INDEX IF NOT EXISTS IX_Enrollments_ClassId   ON Enrollments(ClassId);
CREATE INDEX IF NOT EXISTS IX_Enrollments_StudentId ON Enrollments(StudentId);

CREATE TABLE IF NOT EXISTS Assignments (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    ClassId        INTEGER NOT NULL REFERENCES Classes(Id) ON DELETE RESTRICT,
    Quarter        INTEGER NOT NULL CHECK (Quarter BETWEEN 1 AND 4),
    Name           TEXT    NOT NULL,
    PointsPossible REAL    NOT NULL CHECK (PointsPossible > 0),
    DateCreated    TEXT    NOT NULL DEFAULT (datetime('now'))
);
CREATE INDEX IF NOT EXISTS IX_Assignments_ClassId_Quarter ON Assignments(ClassId, Quarter);

CREATE TABLE IF NOT EXISTS Grades (
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    AssignmentId INTEGER NOT NULL REFERENCES Assignments(Id) ON DELETE CASCADE,
    StudentId    INTEGER NOT NULL REFERENCES Students(Id)    ON DELETE RESTRICT,
    Score        REAL    NOT NULL DEFAULT 0,
    Status       INTEGER NOT NULL DEFAULT 0,
    UNIQUE (AssignmentId, StudentId)
);
CREATE INDEX IF NOT EXISTS IX_Grades_StudentId ON Grades(StudentId);
