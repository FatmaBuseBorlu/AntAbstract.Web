UPDATE Submissions 
SET Status = 3 
WHERE SubmissionId = (SELECT TOP 1 SubmissionId FROM Submissions ORDER BY CreatedAt DESC);
