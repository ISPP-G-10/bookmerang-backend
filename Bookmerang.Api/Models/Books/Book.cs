using Bookmerang.Api.Models.Books;
using Bookmerang.Api.Models.Books.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bookmerang.Api.Models;

[Table("books")]
public class Book
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("owner_id")]
    public Guid OwnerId { get; set; }

    [Column("isbn")]
    public string? Isbn { get; set; }

    [Column("titulo")]
    public string? Titulo { get; set; }

    [Column("autor")]
    public string? Autor { get; set; }

    [Column("editorial")]
    public string? Editorial { get; set; }

    [Column("num_paginas")]
    public int? NumPaginas { get; set; }

    [Column("cover")]
    public CoverType? Cover { get; set; }

    [Column("condition")]
    public BookCondition? Condition { get; set; }

    [Column("observaciones")]
    public string? Observaciones { get; set; }

    [Required]
    [Column("status")]
    public BookStatus Status { get; set; }

    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    // Navigation property apunta a User (tabla "users")
    [ForeignKey("OwnerId")]
    public User Owner { get; set; } = null!;

    public ICollection<BookPhoto> Photos { get; set; } = [];
    public ICollection<BookGenre> BookGenres { get; set; } = [];
    public ICollection<BookLanguage> BookLanguages { get; set; } = [];
}