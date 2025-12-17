UPDATE Submissions 
SET Status = 5 
WHERE SubmissionId = (SELECT TOP 1 SubmissionId FROM Submissions ORDER BY CreatedAt DESC);
