using AntAbstract.Application.DTOs.Submission;
using AntAbstract.Application.DTOs.Conference;
using AntAbstract.Application.Interfaces;
using AntAbstract.Domain.Entities;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AntAbstract.Application.Services
{
    public class SubmissionManager : ISubmissionService
    {
        private readonly IApplicationDbContext _context;
        private readonly IMapper _mapper;

        public SubmissionManager(IApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<SubmissionDto> CreateSubmissionAsync(CreateSubmissionDto input, string userId)
        {
            var submission = _mapper.Map<Submission>(input);

            submission.AuthorId = userId;
            submission.Status = SubmissionStatus.New;
            submission.CreatedDate = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(input.FilePath))
            {
                submission.Files = new List<SubmissionFile>
                {
                    new SubmissionFile
                    {
                        FileName = input.OriginalFileName,
                        StoredFileName = input.StoredFileName,
                        FilePath = input.FilePath,
                        Type = SubmissionFileType.FullText,
                        UploadedAt = DateTime.UtcNow,
                        Version = 1
                    }
                };
            }

            if (input.SubmissionAuthors != null && input.SubmissionAuthors.Any())
            {
                submission.SubmissionAuthors = new List<SubmissionAuthor>();

                foreach (var authorDto in input.SubmissionAuthors)
                {
                    submission.SubmissionAuthors.Add(new SubmissionAuthor
                    {
                        FirstName = authorDto.FirstName,
                        LastName = authorDto.LastName,
                        Email = authorDto.Email,
                        Institution = authorDto.Institution,
                        ORCID = authorDto.ORCID,
                        Order = authorDto.Order,
                        IsCorrespondingAuthor = authorDto.IsCorrespondingAuthor
                    });
                }
            }

            await _context.Submissions.AddAsync(submission);
            await _context.SaveChangesAsync();

            return _mapper.Map<SubmissionDto>(submission);
        }

        public async Task<SubmissionDto> GetSubmissionByIdAsync(Guid id)
        {
            var submission = await _context.Submissions
                .Include(s => s.SubmissionAuthors)
                .Include(s => s.Author)
                .Include(s => s.Files)
                .Include(s => s.Conference)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (submission == null) return null;

            return _mapper.Map<SubmissionDto>(submission);
        }

        public async Task<List<SubmissionDto>> GetMySubmissionsAsync(string userId)
        {
            var list = await _context.Submissions
                .Include(s => s.SubmissionAuthors)
                .Include(s => s.Files)
                .Include(s => s.Conference)
                .Where(s => s.AuthorId == userId)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();

            return _mapper.Map<List<SubmissionDto>>(list);
        }

        public async Task<List<SubmissionDto>> GetAllSubmissionsAsync()
        {
            var list = await _context.Submissions
                .Include(s => s.Author)
                .Include(s => s.Files)
                .Include(s => s.Conference)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();

            return _mapper.Map<List<SubmissionDto>>(list);
        }

        public async Task<List<ConferenceSelectDto>> GetActiveConferencesAsync()
        {
            var conferences = await _context.Conferences
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            return conferences.Select(c => new ConferenceSelectDto
            {
                Id = c.Id,
                Title = c.Title
            }).ToList();
        }

        public async Task UpdateSubmissionAsync(Guid id, CreateSubmissionDto input)
        {
            var submission = await _context.Submissions
                .Include(s => s.SubmissionAuthors)
                .Include(s => s.Files)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (submission == null) throw new Exception("Bildiri bulunamadı.");

            submission.Title = input.Title;
            submission.Abstract = input.Abstract;
            submission.Keywords = input.Keywords;
            submission.UpdatedDate = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(input.FilePath))
            {
                var newVersion = (submission.Files != null && submission.Files.Any())
                                 ? submission.Files.Max(f => f.Version) + 1
                                 : 1;

                var newFile = new SubmissionFile
                {
                    SubmissionId = submission.Id,
                    FileName = input.OriginalFileName,
                    StoredFileName = input.StoredFileName,
                    FilePath = input.FilePath,
                    Type = SubmissionFileType.FullText,
                    Version = newVersion,
                    UploadedAt = DateTime.UtcNow
                };

                submission.Files.Add(newFile);
            }

            _context.SubmissionAuthors.RemoveRange(submission.SubmissionAuthors);

            if (input.SubmissionAuthors != null)
            {
                foreach (var authorDto in input.SubmissionAuthors)
                {
                    var newAuthor = new SubmissionAuthor
                    {
                        SubmissionId = submission.Id,
                        FirstName = authorDto.FirstName,
                        LastName = authorDto.LastName,
                        Email = authorDto.Email,
                        Institution = authorDto.Institution,
                        ORCID = authorDto.ORCID,
                        IsCorrespondingAuthor = authorDto.IsCorrespondingAuthor,
                        Order = authorDto.Order
                    };

                    submission.SubmissionAuthors.Add(newAuthor);
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteSubmissionAsync(Guid id)
        {
            var submission = await _context.Submissions
                .Include(s => s.SubmissionAuthors)
                .Include(s => s.Files)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (submission != null)
            {
                _context.Submissions.Remove(submission);
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateStatusAsync(Guid id, SubmissionStatus newStatus)
        {
            var submission = await _context.Submissions.FirstOrDefaultAsync(s => s.Id == id);

            if (submission != null)
            {
                submission.Status = newStatus;
                submission.UpdatedDate = DateTime.UtcNow;

                if (newStatus == SubmissionStatus.Accepted || newStatus == SubmissionStatus.Rejected)
                {
                    submission.DecisionDate = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
            }
        }
    }
}