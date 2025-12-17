UPDATE Submissions 
SET Status = 1 
WHERE SubmissionId = (SELECT TOP 1 SubmissionId FROM Submissions ORDER BY CreatedAt DESC);
