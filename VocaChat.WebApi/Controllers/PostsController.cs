using Microsoft.AspNetCore.Mvc;
using VocaChat.Models;
using VocaChat.Services;
using VocaChat.WebApi.Dtos.Posts;
using VocaChat.WebApi.Mapping;
using VocaChat.WebApi.Services;

namespace VocaChat.WebApi.Controllers;

/// <summary>提供好友动态、图片、点赞和评论 API。</summary>
[ApiController]
[Route("api/posts")]
public sealed class PostsController : ControllerBase
{
    private readonly PostService _postService;
    private readonly PostMediaService _postMediaService;

    public PostsController(
        PostService postService,
        PostMediaService postMediaService)
    {
        _postService = postService;
        _postMediaService = postMediaService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PostResponse>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<PostResponse>> GetAll()
    {
        return Ok(_postService.GetAllPosts()
            .Select(PostResponseMapper.ToResponse)
            .ToList());
    }

    [HttpGet("{id}", Name = "GetPostById")]
    [ProducesResponseType(typeof(PostResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<PostResponse> GetById(Guid id)
    {
        Post? post = _postService.FindById(id);
        return post is null
            ? NotFound()
            : Ok(PostResponseMapper.ToResponse(post));
    }

    [HttpPost]
    [ProducesResponseType(typeof(PostResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<PostResponse> Create([FromBody] CreatePostRequest request)
    {
        if (!_postService.TryCreatePost(
                request.AuthorAiAccountId,
                request.Content,
                out Post? post,
                out string errorMessage)
            || post is null)
        {
            return BadRequest(new { message = errorMessage });
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = post.Id },
            PostResponseMapper.ToResponse(post));
    }

    [HttpPost("{id}/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PostMediaService.MaximumImageLength + 1024 * 1024)]
    [ProducesResponseType(typeof(PostImageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<PostImageResponse>> UploadImage(
        Guid id,
        [FromForm] UploadPostImageRequest request,
        CancellationToken cancellationToken)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return BadRequest(new { message = "请选择一个非空图片文件。" });
        }

        await using Stream source = request.File.OpenReadStream();
        PostMediaUploadResult result = await _postMediaService.UploadAsync(
            id,
            source,
            request.File.Length,
            cancellationToken);

        return result.Status switch
        {
            PostMediaUploadStatus.Succeeded => StatusCode(
                StatusCodes.Status201Created,
                PostResponseMapper.ToImageResponse(result.Image!)),
            PostMediaUploadStatus.PostNotFound => NotFound(),
            PostMediaUploadStatus.TooLarge => StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                new { message = result.ErrorMessage }),
            PostMediaUploadStatus.StorageFailed => StatusCode(
                StatusCodes.Status500InternalServerError,
                new { message = result.ErrorMessage }),
            PostMediaUploadStatus.PersistenceFailed => BadRequest(
                new { message = result.ErrorMessage }),
            _ => BadRequest(new { message = result.ErrorMessage })
        };
    }

    [HttpGet("{postId}/images/{imageId}")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetImage(Guid postId, Guid imageId)
    {
        StoredMediaContent? content = _postMediaService.OpenRead(postId, imageId);
        return content is null
            ? NotFound()
            : File(content.Stream, content.ContentType, enableRangeProcessing: true);
    }

    [HttpPut("{id}/like")]
    [ProducesResponseType(typeof(PostResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<PostResponse> Like(Guid id)
    {
        if (!_postService.TryAddLocalUserLike(id, out string errorMessage))
        {
            return NotFound(new { message = errorMessage });
        }

        return Ok(PostResponseMapper.ToResponse(_postService.FindById(id)!));
    }

    [HttpDelete("{id}/like")]
    [ProducesResponseType(typeof(PostResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<PostResponse> Unlike(Guid id)
    {
        if (!_postService.TryRemoveLocalUserLike(id, out string errorMessage))
        {
            return NotFound(new { message = errorMessage });
        }

        return Ok(PostResponseMapper.ToResponse(_postService.FindById(id)!));
    }

    [HttpPost("{id}/comments")]
    [ProducesResponseType(typeof(PostResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<PostResponse> Comment(
        Guid id,
        [FromBody] CreatePostCommentRequest request)
    {
        if (_postService.FindById(id) is null)
        {
            return NotFound();
        }

        if (!_postService.TryAddLocalUserComment(
                id,
                request.Content,
                out _,
                out string errorMessage))
        {
            return BadRequest(new { message = errorMessage });
        }

        return StatusCode(
            StatusCodes.Status201Created,
            PostResponseMapper.ToResponse(_postService.FindById(id)!));
    }
}
