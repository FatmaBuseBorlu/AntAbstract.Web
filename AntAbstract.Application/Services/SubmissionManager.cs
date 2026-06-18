using AntAbstract.Application.DTOs.Conference;
using AntAbstract.Application.DTOs.Submission;
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
            var submission = new Submission
            {
                Id = Guid.NewGuid(),

                ConferenceId = input.ConferenceId,
                ConferenceTopicId = input.ConferenceTopicId,

                AuthorId = userId,

                Title = input.Title,
                Abstract = input.Abstract,
                Keywords = input.Keywords,
                Topic = input.Topic,
                PresentationType = input.PresentationType,

                Status = SubmissionStatus.New,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = null
            };

            if (!string.IsNullOrWhiteSpace(input.FilePath))
            {
                submission.Files = new List<SubmissionFile>
                {
                    new SubmissionFile
                    {
                        SubmissionId = submission.Id,
                        FileName = input.OriginalFileName!,
                        StoredFileName = input.StoredFileName!,
                        FilePath = input.FilePath!,
                        Type = SubmissionFileType.FullText,
                        UploadedAt = DateTime.UtcNow,
                        Version = 1
                    }
                };
            }

            if (input.SubmissionAuthors != null && input.SubmissionAuthors.Any())
            {
                submission.SubmissionAuthors = new List<SubmissionAuthor>();

                foreach (var authorDto in input.SubmissionAuthors.OrderBy(a => a.Order))
                {
                    submission.SubmissionAuthors.Add(new SubmissionAuthor
                    {
                        SubmissionId = submission.Id,

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

        public async Task<SubmissionDto?> GetSubmissionByIdAsync(Guid id)
        {
            var submission = await _context.Submissions
                .Include(s => s.SubmissionAuthors)
                .Include(s => s.Author)
                .Include(s => s.Files)
                .Include(s => s.Conference)
                .Include(s => s.ConferenceTopic)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (submission == null)
            {
                return null;
            }

            return _mapper.Map<SubmissionDto>(submission);
        }

        public async Task<List<SubmissionDto>> GetMySubmissionsAsync(string userId)
        {
            // Hem baş yazar (AuthorId) hem de ortak yazar (SubmissionAuthor.AppUserId) olduğu bildiriler
            var list = await _context.Submissions
                .Include(s => s.SubmissionAuthors)
                .Include(s => s.Files)
                .Include(s => s.Conference)
                .Include(s => s.ConferenceTopic)
                .Where(s =>
                    s.AuthorId == userId ||
                    s.SubmissionAuthors.Any(a => a.AppUserId == userId))
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();

            var dtoList = _mapper.Map<List<SubmissionDto>>(list);

            foreach (var dto in dtoList)
            {
                var entity = list.FirstOrDefault(x => x.Id == dto.Id);

                if (entity != null)
                {
                    dto.ConferenceId = entity.ConferenceId;
                    dto.Topic = entity.Topic;
                }
            }

            return dtoList;
        }

        public async Task<List<SubmissionDto>> GetAllSubmissionsAsync()
        {
            var list = await _context.Submissions
                .Include(s => s.Author)
                .Include(s => s.Files)
                .Include(s => s.Conference)
                .Include(s => s.ConferenceTopic)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync();

            var dtoList = _mapper.Map<List<SubmissionDto>>(list);

            foreach (var dto in dtoList)
            {
                var entity = list.FirstOrDefault(x => x.Id == dto.Id);

                if (entity != null)
                {
                    dto.ConferenceId = entity.ConferenceId;
                    dto.Topic = entity.Topic;
                }
            }

            return dtoList;
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

            if (submission == null)
            {
                throw new Exception("Bildiri bulunamadı.");
            }

            submission.ConferenceTopicId = input.ConferenceTopicId;
            submission.Title = input.Title;
            submission.Abstract = input.Abstract;
            submission.Keywords = input.Keywords;
            submission.Topic = input.Topic;
            submission.PresentationType = input.PresentationType;
            submission.UpdatedDate = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(input.FilePath))
            {
                var newVersion = submission.Files != null && submission.Files.Any()
                    ? submission.Files.Max(f => f.Version) + 1
                    : 1;

                var newFile = new SubmissionFile
                {
                    SubmissionId = submission.Id,
                    FileName = input.OriginalFileName!,
                    StoredFileName = input.StoredFileName!,
                    FilePath = input.FilePath!,
                    Type = SubmissionFileType.FullText,
                    Version = newVersion,
                    UploadedAt = DateTime.UtcNow
                };

                submission.Files!.Add(newFile);
            }

            if (submission.SubmissionAuthors != null)
            {
                submission.SubmissionAuthors.Clear();
            }
            else
            {
                submission.SubmissionAuthors = new List<SubmissionAuthor>();
            }

            if (input.SubmissionAuthors != null)
            {
                foreach (var authorDto in input.SubmissionAuthors.OrderBy(a => a.Order))
                {
                    submission.SubmissionAuthors.Add(new SubmissionAuthor
                    {
                        SubmissionId = submission.Id,

                        FirstName = authorDto.FirstName,
                        LastName = authorDto.LastName,
                        Email = authorDto.Email,
                        Institution = authorDto.Institution,
                        ORCID = authorDto.ORCID,

                        IsCorrespondingAuthor = authorDto.IsCorrespondingAuthor,
                        Order = authorDto.Order
                    });
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
            var submission = await _context.Submissions
                .FirstOrDefaultAsync(s => s.Id == id);

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
