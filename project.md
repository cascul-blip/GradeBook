The goal here is to create an application that will allow me to enter grades for students that I am teaching.

It needs to be a GUI
It needs to run in Windows as well as Linux

This is a small school, so we probably don't need a large database to store and pull records.  Around 20 students for 10 classes, so something like mysql would be overkill.  I would like to keep dependencies minimal if possible. 

The project needs to track grades for multiple students in different classes.
  For example, Micah can be in Math 87 as well as Earth Science

It needs to be able to assign a blank grade to everyone in a class at once when the homework is assigned, then when they are turned in I can fill in the grade for each student.  This will let me track which grades have not been turned in.  For example.  if I assign lesson 3 in Math 87, it needs to add that record to Micah, Prentiss, Cohen, and Baleigh automatically.  It will ask the point value for that assignment (let's say 30 points).  Then each student in that class will have a 0 out of 30 points in that assignment.  If Micah and Prentiss turn in the homework, then I will change the 0 to a 30 or whatever they score, and leave the students that have not turned in the homework with a 0.

Each assignment needs to be in one of 4 states.  Completed, Uncompleted, Late, and excused.  If the state is excused, then the grade on that assignment should not count for or against that student.

All assignments and grades need to be able to be assigned into one of the 4 quarters.  Each quarter will be separately tracked but can be averaged into the semester and final grades.


Reporting:
It needs to be exportable into a format that is visually understandable and printable.
It will have a class report.
  The class report will be selectable for each class.  "Math 87", "Biology", etc...
  The class report will give a list of every student in that class as well as the grade that they are getting in the class, and the number of missing uncompleted assignments

It will have a student report.
  The student report will have a grade for every class that they are in.
  It will include a list of missing assignments that they have not completed.
