# R2Library XML Content Validation Specification

**Version:** 1.0  
**Date:** January 17, 2026  
**Purpose:** XML validation rules for book content conversion to R2Library format

---

## Table of Contents

1. [Overview](#1-overview)
2. [File Structure Requirements](#2-file-structure-requirements)
3. [File Naming Conventions](#3-file-naming-conventions)
4. [The Three Golden Rules](#4-the-three-golden-rules)
5. [XML Schema Specifications](#5-xml-schema-specifications)
6. [Validation Rules by File Type](#6-validation-rules-by-file-type)
7. [Content Type Mapping](#7-content-type-mapping)
8. [Common Errors and Solutions](#8-common-errors-and-solutions)
9. [Pre-Submission Checklist](#9-pre-submission-checklist)
10. [Validation Examples](#10-validation-examples)

---

## 1. Overview

### 1.1 Purpose

This document defines the validation rules for XML content packages submitted to the R2Library book loading system. Following these rules ensures successful processing through:

- Java Book Loader (initial content processing)
- TransformXmlTask (XML to HTML conversion)
- DtSearchHtmlIndexerTask (search index generation)
- BookLoaderPostProcessingTask (metadata and licensing)

### 1.2 Critical Success Factors

| Factor | Requirement |
|--------|-------------|
| File naming | Must follow exact patterns |
| Root elements | Must match filename prefix |
| Section IDs | Must be consistent across filename, XML, and content type |
| XML validity | Must be well-formed and properly encoded |

### 1.3 Consequences of Validation Failures

| Failure Type | Impact |
|--------------|--------|
| Filename mismatch | Transform fails, book not published |
| Invalid XML | Parse error, entire book fails |
| Missing required elements | Incomplete metadata, search issues |
| Section ID inconsistency | Wrong XSL template applied, transform fails |

---

## 2. File Structure Requirements

### 2.1 Required Directory Structure

```
{ISBN}/
├── book.{ISBN}.xml              # REQUIRED - Book metadata
├── toc.{ISBN}.xml               # REQUIRED - Table of contents
├── sect1.{ISBN}.{id}.xml        # REQUIRED - At least one section file
├── preface.{ISBN}.{id}.xml      # OPTIONAL - Preface content
├── appendix.{ISBN}.{id}.xml     # OPTIONAL - Appendix content
├── dedication.{ISBN}.{id}.xml   # OPTIONAL - Dedication content
└── [additional section files]
```

### 2.2 Minimum Required Files

| File | Required | Notes |
|------|----------|-------|
| `book.{ISBN}.xml` | **YES** | Core metadata file |
| `toc.{ISBN}.xml` | **YES** | Table of contents |
| At least one content file | **YES** | sect1/preface/appendix/dedication |

### 2.3 File Count Expectations

- Typical book: 50-500 section files
- Large reference book: 500-2000+ section files
- All files must be in the same directory (no subdirectories)

---

## 3. File Naming Conventions

### 3.1 ISBN Requirements

| Rule | Specification | Example |
|------|---------------|---------|
| Length | Exactly 13 digits | `9781683674832` |
| Format | Numbers only | NOT `978-1-68367-483-2` |
| Consistency | Same ISBN in ALL files | All files use `9781683674832` |

### 3.2 File Naming Patterns

| File Type | Pattern | Regex Validation |
|-----------|---------|------------------|
| Book metadata | `book.{ISBN}.xml` | `^book\.\d{13}\.xml$` |
| Table of contents | `toc.{ISBN}.xml` | `^toc\.\d{13}\.xml$` |
| Section | `sect1.{ISBN}.{sectionId}.xml` | `^sect1\.\d{13}\.[a-zA-Z0-9]+\.xml$` |
| Preface | `preface.{ISBN}.{sectionId}.xml` | `^preface\.\d{13}\.[a-zA-Z0-9]+\.xml$` |
| Appendix | `appendix.{ISBN}.{sectionId}.xml` | `^appendix\.\d{13}\.[a-zA-Z0-9]+\.xml$` |
| Dedication | `dedication.{ISBN}.{sectionId}.xml` | `^dedication\.\d{13}\.[a-zA-Z0-9]+\.xml$` |

### 3.3 Section ID Format

| Content Type | Section ID Pattern | Examples |
|--------------|-------------------|----------|
| Chapter/Section | `ch{NNNN}` or `ch{NNNN}s{NNNN}` | `ch0001`, `ch0001s0001`, `ch0012s0003` |
| Preface | `pr{NN}` or `pref{NN}` | `pr01`, `pref01` |
| Appendix | `ap{NN}` or `app{NN}` | `ap01`, `app01`, `appendixA` |
| Dedication | `dd{NN}` or `ded{NN}` | `dd01`, `ded01` |
| Glossary | `gl{NN}` or `gloss{NN}` | `gl01`, `glossary01` |
| Bibliography | `bi{NN}` or `bib{NN}` | `bi01`, `bib01` |

### 3.4 Naming Examples

**VALID:**
```
book.9781683674832.xml
toc.9781683674832.xml
sect1.9781683674832.ch0001s0001.xml
sect1.9781683674832.ch0001s0002.xml
sect1.9781683674832.ch0002s0001.xml
preface.9781683674832.pr01.xml
appendix.9781683674832.ap01.xml
dedication.9781683674832.ded01.xml
```

**INVALID:**
```
book.978-1-68367-483-2.xml          # Hyphens in ISBN
Book.9781683674832.xml              # Uppercase prefix
sect1.9781683674832.chapter1.xml    # Non-standard section ID
preface.9781683674832.ch0001.xml    # Section ID doesn't match content type
9781683674832_book.xml              # Wrong format
```

---

## 4. The Three Golden Rules

### RULE 1: Filename Prefix MUST Match XML Root Element

```
┌─────────────────────────────────────────────────────────────────┐
│  FILENAME PREFIX          →    XML ROOT ELEMENT                 │
│  ─────────────────────────────────────────────────────────────  │
│  sect1.{ISBN}.*.xml       →    <sect1>                          │
│  preface.{ISBN}.*.xml     →    <preface>                        │
│  appendix.{ISBN}.*.xml    →    <appendix>                       │
│  dedication.{ISBN}.*.xml  →    <dedication>                     │
│  book.{ISBN}.xml          →    <book>                           │
│  toc.{ISBN}.xml           →    <toc>                            │
└─────────────────────────────────────────────────────────────────┘
```

**Example of VIOLATION:**
```xml
<!-- File: preface.9781683674832.ch0001.xml -->
<!-- WRONG: Root element doesn't match filename prefix -->
<sect1 id="ch0001">
    <title>Introduction</title>
</sect1>

<!-- CORRECT: Either change filename OR change root element -->
```

### RULE 2: Section ID Prefix MUST Match Content Type

The first 2 characters of the section ID determine which XSL template is used:

| Section ID Prefix | Content Type | Required Filename Prefix |
|-------------------|--------------|-------------------------|
| `ch` | Chapter/Book | `sect1.` |
| `pr` | Preface | `preface.` |
| `ap` | Appendix | `appendix.` |
| `dd` or `de` | Dedication | `dedication.` |
| `gl` | Glossary | `sect1.` (processed from book.xml) |
| `bi` | Bibliography | `sect1.` |

### RULE 3: All Three Must Be Consistent

```
┌─────────────────────────────────────────────────────────────────┐
│                    TRIPLE CONSISTENCY CHECK                     │
│                                                                 │
│   FILENAME PREFIX  ←──→  ROOT ELEMENT  ←──→  SECTION ID PREFIX  │
│                                                                 │
│   All three must indicate the SAME content type!                │
└─────────────────────────────────────────────────────────────────┘
```

**CORRECT Examples:**

| Filename | Root Element | Section ID | Content Type |
|----------|--------------|------------|--------------|
| `sect1.{ISBN}.ch0001.xml` | `<sect1>` | `ch0001` | Chapter ✓ |
| `preface.{ISBN}.pr01.xml` | `<preface>` | `pr01` | Preface ✓ |
| `appendix.{ISBN}.ap01.xml` | `<appendix>` | `ap01` | Appendix ✓ |
| `dedication.{ISBN}.ded01.xml` | `<dedication>` | `ded01` | Dedication ✓ |

**INCORRECT Examples:**

| Filename | Root Element | Section ID | Problem |
|----------|--------------|------------|---------|
| `preface.{ISBN}.ch0001.xml` | `<sect1>` | `ch0001` | ❌ All three disagree |
| `sect1.{ISBN}.pr01.xml` | `<sect1>` | `pr01` | ❌ Section ID suggests preface |
| `appendix.{ISBN}.ch0001.xml` | `<appendix>` | `ch0001` | ❌ Section ID suggests chapter |

---

## 5. XML Schema Specifications

### 5.1 book.{ISBN}.xml

```xml
<?xml version="1.0" encoding="UTF-8"?>
<book>
    <bookinfo>
        <!-- ═══════════════════════════════════════════════════════════ -->
        <!-- REQUIRED ELEMENTS                                            -->
        <!-- ═══════════════════════════════════════════════════════════ -->
        
        <title>Book Title Here</title>
        
        <!-- ═══════════════════════════════════════════════════════════ -->
        <!-- RECOMMENDED ELEMENTS                                         -->
        <!-- ═══════════════════════════════════════════════════════════ -->
        
        <subtitle>Subtitle if applicable</subtitle>
        
        <copyright>
            <year>2024</year>
            <holder>Publisher Name</holder>
        </copyright>
        
        <primaryauthor>
            <personname>
                <firstname>First</firstname>
                <othername role="mi">M</othername>
                <surname>Last</surname>
                <lineage>Jr.</lineage>
                <degree>MD, PhD</degree>
            </personname>
        </primaryauthor>
        
        <!-- ═══════════════════════════════════════════════════════════ -->
        <!-- OPTIONAL ELEMENTS                                            -->
        <!-- ═══════════════════════════════════════════════════════════ -->
        
        <authorgroup>
            <author>
                <personname>
                    <firstname>Jane</firstname>
                    <surname>Doe</surname>
                    <degree>RN, MSN</degree>
                </personname>
            </author>
            <!-- Additional authors -->
        </authorgroup>
        
        <editor>
            <personname>
                <firstname>Editor</firstname>
                <surname>Name</surname>
                <degree>MD</degree>
            </personname>
            <affiliation>
                <jobtitle>Professor of Medicine</jobtitle>
                <orgname>University Medical Center</orgname>
            </affiliation>
        </editor>
        <!-- Additional editors -->
        
    </bookinfo>
    
    <!-- ═══════════════════════════════════════════════════════════════ -->
    <!-- CHAPTER DEFINITIONS (Required for TOC linking)                   -->
    <!-- ═══════════════════════════════════════════════════════════════ -->
    
    <chapter id="ch01" label="1">
        <title>Chapter One Title</title>
    </chapter>
    
    <chapter id="ch02" label="2">
        <title>Chapter Two Title</title>
    </chapter>
    
    <!-- ═══════════════════════════════════════════════════════════════ -->
    <!-- GLOSSARY DEFINITIONS (Optional)                                  -->
    <!-- ═══════════════════════════════════════════════════════════════ -->
    
    <glossary id="glossary01">
        <!-- Glossary content -->
    </glossary>
    
</book>
```

### 5.2 toc.{ISBN}.xml

```xml
<?xml version="1.0" encoding="UTF-8"?>
<toc>
    <!-- Each tocentry links to a chapter/section by id -->
    <tocentry linkend="pr01">Preface</tocentry>
    <tocentry linkend="ch01">Chapter 1: Introduction</tocentry>
    <tocentry linkend="ch02">Chapter 2: Fundamentals</tocentry>
    <tocentry linkend="ch03">Chapter 3: Advanced Topics</tocentry>
    <tocentry linkend="ap01">Appendix A: Reference Tables</tocentry>
    <tocentry linkend="glossary01">Glossary</tocentry>
</toc>
```

### 5.3 Section Files (sect1.{ISBN}.{id}.xml)

```xml
<?xml version="1.0" encoding="UTF-8"?>
<sect1 id="ch0001s0001">
    <!-- ═══════════════════════════════════════════════════════════════ -->
    <!-- SECTION TITLE (Recommended)                                      -->
    <!-- ═══════════════════════════════════════════════════════════════ -->
    
    <title>Section Title</title>
    
    <!-- ═══════════════════════════════════════════════════════════════ -->
    <!-- CHAPTER INFO FOR META TAGS (Recommended for search)              -->
    <!-- ═══════════════════════════════════════════════════════════════ -->
    
    <risinfo>
        <chaptertitle>Parent Chapter Title</chaptertitle>
        <chapterid>ch0001</chapterid>
        <chapternumber>1</chapternumber>
    </risinfo>
    
    <!-- ═══════════════════════════════════════════════════════════════ -->
    <!-- INDEX TERMS FOR SEARCH (Recommended)                             -->
    <!-- ═══════════════════════════════════════════════════════════════ -->
    
    <risindex>
        <risterm>medical term</risterm>
        <risterm>drug name</risterm>
        <risterm>disease name</risterm>
        <risterm>procedure</risterm>
    </risindex>
    
    <!-- ═══════════════════════════════════════════════════════════════ -->
    <!-- CONTENT                                                          -->
    <!-- ═══════════════════════════════════════════════════════════════ -->
    
    <para>Content paragraph...</para>
    
    <figure id="fig001">
        <title>Figure Title</title>
        <mediaobject>
            <imageobject>
                <imagedata fileref="images/figure001.png"/>
            </imageobject>
        </mediaobject>
    </figure>
    
    <table id="tbl001">
        <title>Table Title</title>
        <!-- table content -->
    </table>
    
</sect1>
```

### 5.4 Preface File (preface.{ISBN}.{id}.xml)

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!-- NOTE: Root element MUST be <preface>, NOT <sect1> -->
<preface id="pr01">
    <title>Preface</title>
    
    <risinfo>
        <chaptertitle>Preface</chaptertitle>
        <chapterid>pr01</chapterid>
    </risinfo>
    
    <para>Preface content...</para>
</preface>
```

### 5.5 Appendix File (appendix.{ISBN}.{id}.xml)

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!-- NOTE: Root element MUST be <appendix>, NOT <sect1> -->
<appendix id="ap01">
    <title>Appendix A: Reference Tables</title>
    
    <risinfo>
        <chaptertitle>Appendix A</chaptertitle>
        <chapterid>ap01</chapterid>
    </risinfo>
    
    <para>Appendix content...</para>
</appendix>
```

### 5.6 Dedication File (dedication.{ISBN}.{id}.xml)

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!-- NOTE: Root element MUST be <dedication>, NOT <sect1> -->
<dedication id="ded01">
    <title>Dedication</title>
    
    <para>Dedication content...</para>
</dedication>
```

---

## 6. Validation Rules by File Type

### 6.1 book.{ISBN}.xml Validation

| Rule ID | Element/Attribute | Required | Validation |
|---------|-------------------|----------|------------|
| B001 | Root `<book>` | YES | Must be root element |
| B002 | `<bookinfo>` | YES | Must exist as child of `<book>` |
| B003 | `<bookinfo><title>` | YES | Must exist and not be empty |
| B004 | `<copyright><year>` | NO | If present, must be numeric (1900-2100) |
| B005 | `<copyright><holder>` | NO | If present, must not be empty |
| B006 | `<primaryauthor><personname><surname>` | NO | If author exists, surname required |
| B007 | `<othername role="">` | NO | role attribute must be "mi" |
| B008 | `<chapter id="">` | NO | id attribute required on each chapter |
| B009 | `<chapter label="">` | NO | label attribute required for TOC numbering |
| B010 | `<glossary id="">` | NO | id attribute required if glossary exists |
| B011 | Chapter id values | NO | Must be unique within document |

### 6.2 toc.{ISBN}.xml Validation

| Rule ID | Element/Attribute | Required | Validation |
|---------|-------------------|----------|------------|
| T001 | Root `<toc>` | YES | Must be root element |
| T002 | `<tocentry>` | YES | At least one must exist |
| T003 | `<tocentry linkend="">` | YES | linkend attribute required |
| T004 | linkend value | YES | Must match an id in book.xml chapters |
| T005 | tocentry text | YES | Must not be empty |

### 6.3 Section File Validation

| Rule ID | Check | Required | Validation |
|---------|-------|----------|------------|
| S001 | Root element | YES | Must match filename prefix |
| S002 | `id` attribute | RECOMMENDED | Should exist on root element |
| S003 | id value | RECOMMENDED | Should match section ID in filename |
| S004 | `<title>` | RECOMMENDED | Should exist and not be empty |
| S005 | `<risinfo>` | RECOMMENDED | Should exist for search metadata |
| S006 | `<risindex>` | RECOMMENDED | Should exist with search terms |
| S007 | Section ID prefix | YES | Must match content type (see Rule 2) |

### 6.4 Cross-File Validation

| Rule ID | Check | Required | Validation |
|---------|-------|----------|------------|
| X001 | ISBN consistency | YES | Same ISBN in all filenames |
| X002 | TOC linkend refs | YES | All linkend values must have matching chapter ids |
| X003 | Chapter coverage | RECOMMENDED | All chapters should have section files |
| X004 | No orphan sections | RECOMMENDED | All sections should link to TOC entries |

---

## 7. Content Type Mapping

### 7.1 Section ID Prefix to Content Type

The transform system determines content type from the **first 2 characters** of the section ID:

```
┌────────────────┬─────────────────┬────────────────────────────────┐
│ Section ID     │ Content Type    │ XSL Template Used              │
│ Prefix         │                 │                                │
├────────────────┼─────────────────┼────────────────────────────────┤
│ ap             │ Appendix        │ appendix.xsl                   │
│ dd             │ Dedication      │ dedication.xsl                 │
│ de             │ Dedication      │ dedication.xsl                 │
│ pr             │ Preface         │ preface.xsl                    │
│ gl             │ Glossary        │ glossary.xsl                   │
│ bi             │ Bibliography    │ bibliography.xsl               │
│ ch             │ Book (default)  │ book.xsl                       │
│ (other)        │ Book (default)  │ book.xsl                       │
└────────────────┴─────────────────┴────────────────────────────────┘
```

### 7.2 Required Consistency Matrix

| Content Type | Filename Prefix | Root Element | Section ID Starts With |
|--------------|-----------------|--------------|------------------------|
| Chapter | `sect1.` | `<sect1>` | `ch` |
| Preface | `preface.` | `<preface>` | `pr` |
| Appendix | `appendix.` | `<appendix>` | `ap` |
| Dedication | `dedication.` | `<dedication>` | `dd` or `de` |
| Glossary | (in book.xml) | `<glossary>` | `gl` |
| Bibliography | `sect1.` | `<sect1>` | `bi` |

---

## 8. Common Errors and Solutions

### 8.1 Error: Filename-Root Element Mismatch

**Symptom:**
```
Error transforming file: preface.9781683674832.ch0001.xml
```

**Cause:** File is named `preface.*` but contains `<sect1>` root element.

**Solution Options:**

Option A - Fix the filename:
```
BEFORE: preface.9781683674832.ch0001.xml
AFTER:  sect1.9781683674832.ch0001.xml
```

Option B - Fix the XML content:
```xml
<!-- BEFORE -->
<sect1 id="ch0001">...</sect1>

<!-- AFTER -->
<preface id="pr01">...</preface>
```

### 8.2 Error: Section ID Inconsistency

**Symptom:**
```
Wrong XSL template applied, incorrect HTML output
```

**Cause:** Section ID prefix doesn't match expected content type.

**Solution:**
```
BEFORE: preface.9781683674832.ch0001.xml (ch = chapter)
AFTER:  preface.9781683674832.pr01.xml   (pr = preface)
```

### 8.3 Error: Missing Required Elements

**Symptom:**
```
Empty metadata in search results
```

**Cause:** Missing `<title>`, `<risinfo>`, or `<risindex>` elements.

**Solution:** Add required elements:
```xml
<sect1 id="ch0001s0001">
    <title>Section Title</title>        <!-- ADD THIS -->
    <risinfo>                           <!-- ADD THIS -->
        <chaptertitle>Chapter Title</chaptertitle>
        <chapterid>ch0001</chapterid>
        <chapternumber>1</chapternumber>
    </risinfo>
    <risindex>                          <!-- ADD THIS -->
        <risterm>keyword1</risterm>
        <risterm>keyword2</risterm>
    </risindex>
    <para>Content...</para>
</sect1>
```

### 8.4 Error: Invalid Copyright Year

**Symptom:**
```
CopyrightYear: 0
```

**Cause:** Non-numeric or malformed year value.

**Valid formats:**
```xml
<year>2024</year>           <!-- OK -->
<year>c2024</year>          <!-- OK - 'c' stripped -->
<year>2023, 2024</year>     <!-- OK - larger year used -->
```

**Invalid formats:**
```xml
<year>Twenty Twenty</year>  <!-- INVALID -->
<year></year>               <!-- INVALID -->
```

### 8.5 Error: ISBN Mismatch

**Symptom:**
```
Files not found during processing
```

**Cause:** Different ISBNs in different files.

**Solution:** Ensure ALL files use identical ISBN:
```
✓ book.9781683674832.xml
✓ toc.9781683674832.xml
✓ sect1.9781683674832.ch0001.xml
✗ sect1.9781683674833.ch0002.xml  <!-- WRONG ISBN -->
```

---

## 9. Pre-Submission Checklist

### 9.1 File Structure Checklist

```
□ Directory contains book.{ISBN}.xml
□ Directory contains toc.{ISBN}.xml
□ Directory contains at least one content file (sect1/preface/appendix/dedication)
□ All files are in the same directory (no subdirectories for XML)
□ No duplicate filenames
```

### 9.2 ISBN Checklist

```
□ ISBN is exactly 13 digits
□ ISBN contains no hyphens, spaces, or letters
□ Same ISBN used in ALL filenames
□ ISBN matches the book being converted
```

### 9.3 Filename Checklist

```
□ All filenames are lowercase (except ISBN digits)
□ All filenames use periods (.) as separators
□ All filenames end with .xml
□ No spaces in filenames
□ No special characters in filenames
```

### 9.4 The Three Golden Rules Checklist

```
□ RULE 1: Every file's prefix matches its root element
  □ sect1.* files have <sect1> root
  □ preface.* files have <preface> root
  □ appendix.* files have <appendix> root
  □ dedication.* files have <dedication> root
  □ book.* file has <book> root
  □ toc.* file has <toc> root

□ RULE 2: Every section ID prefix matches content type
  □ Chapter files use ch* section IDs
  □ Preface files use pr* section IDs
  □ Appendix files use ap* section IDs
  □ Dedication files use dd* or de* section IDs

□ RULE 3: Triple consistency verified for all files
  □ Filename prefix = Root element = Section ID prefix (content type)
```

### 9.5 book.xml Checklist

```
□ Root element is <book>
□ <bookinfo> element exists
□ <title> element exists and is not empty
□ <copyright><year> is numeric (if present)
□ All <chapter> elements have id attribute
□ All <chapter> elements have label attribute
□ Chapter id values are unique
□ <othername role="mi"> uses correct role value
```

### 9.6 toc.xml Checklist

```
□ Root element is <toc>
□ At least one <tocentry> exists
□ All <tocentry> elements have linkend attribute
□ All linkend values match chapter ids in book.xml
□ No empty <tocentry> elements
```

### 9.7 Section Files Checklist

```
□ Root element matches filename prefix
□ id attribute on root element matches filename section ID
□ <title> element exists
□ <risinfo> element exists with chapter context
□ <risindex> element exists with search terms
□ No malformed XML (all tags properly closed)
```

### 9.8 XML Technical Checklist

```
□ All files have XML declaration: <?xml version="1.0" encoding="UTF-8"?>
□ All files are UTF-8 encoded
□ No BOM (Byte Order Mark) at file start
□ All special characters properly escaped (&amp; &lt; &gt;)
□ All attribute values properly quoted
□ No control characters in content
□ All files parse successfully as XML
```

---

## 10. Validation Examples

### 10.1 CORRECT: Standard Chapter File

**Filename:** `sect1.9781683674832.ch0001s0001.xml`

```xml
<?xml version="1.0" encoding="UTF-8"?>
<sect1 id="ch0001s0001">
    <title>Introduction to Clinical Microbiology</title>
    
    <risinfo>
        <chaptertitle>Chapter 1: Fundamentals</chaptertitle>
        <chapterid>ch0001</chapterid>
        <chapternumber>1</chapternumber>
    </risinfo>
    
    <risindex>
        <risterm>microbiology</risterm>
        <risterm>bacteria</risterm>
        <risterm>culture techniques</risterm>
    </risindex>
    
    <para>Clinical microbiology is the study of...</para>
</sect1>
```

**Validation Result:** ✅ PASS
- Filename prefix `sect1` matches root element `<sect1>` ✓
- Section ID `ch0001s0001` starts with `ch` (chapter) ✓
- All three indicate "chapter" content type ✓

### 10.2 CORRECT: Preface File

**Filename:** `preface.9781683674832.pr01.xml`

```xml
<?xml version="1.0" encoding="UTF-8"?>
<preface id="pr01">
    <title>Preface to the Fourth Edition</title>
    
    <risinfo>
        <chaptertitle>Preface</chaptertitle>
        <chapterid>pr01</chapterid>
    </risinfo>
    
    <para>Welcome to the fourth edition...</para>
</preface>
```

**Validation Result:** ✅ PASS
- Filename prefix `preface` matches root element `<preface>` ✓
- Section ID `pr01` starts with `pr` (preface) ✓
- All three indicate "preface" content type ✓

### 10.3 INCORRECT: Mismatched Filename and Root

**Filename:** `preface.9781683674832.ch0001.xml`

```xml
<?xml version="1.0" encoding="UTF-8"?>
<sect1 id="ch0001">
    <title>Introduction</title>
    <para>Content...</para>
</sect1>
```

**Validation Result:** ❌ FAIL

| Check | Expected | Actual | Result |
|-------|----------|--------|--------|
| Filename prefix | preface | preface | - |
| Root element | preface | sect1 | ❌ MISMATCH |
| Section ID prefix | pr | ch | ❌ MISMATCH |

**Errors:**
1. Filename says `preface` but root element is `<sect1>`
2. Section ID `ch0001` indicates chapter, not preface
3. Triple consistency FAILED

**Fix Options:**
- Change filename to `sect1.9781683674832.ch0001.xml`
- OR change root to `<preface>` and section ID to `pr01`

### 10.4 INCORRECT: Section ID Mismatch

**Filename:** `appendix.9781683674832.ch0001.xml`

```xml
<?xml version="1.0" encoding="UTF-8"?>
<appendix id="ch0001">
    <title>Appendix A</title>
    <para>Reference tables...</para>
</appendix>
```

**Validation Result:** ❌ FAIL

| Check | Expected | Actual | Result |
|-------|----------|--------|--------|
| Filename prefix | appendix | appendix | ✓ |
| Root element | appendix | appendix | ✓ |
| Section ID prefix | ap | ch | ❌ MISMATCH |

**Error:** Section ID `ch0001` will cause wrong XSL template selection.

**Fix:** Change section ID to `ap01`:
- Filename: `appendix.9781683674832.ap01.xml`
- XML: `<appendix id="ap01">`

---

## Appendix A: Quick Reference Card

```
╔═══════════════════════════════════════════════════════════════════════════╗
║                     R2LIBRARY XML QUICK REFERENCE                         ║
╠═══════════════════════════════════════════════════════════════════════════╣
║                                                                           ║
║  FILE NAMING:  {prefix}.{ISBN}.{sectionId}.xml                            ║
║                                                                           ║
║  ┌──────────────┬─────────────┬──────────────┬─────────────────────────┐  ║
║  │ Content Type │ Prefix      │ Root Element │ Section ID Prefix       │  ║
║  ├──────────────┼─────────────┼──────────────┼─────────────────────────┤  ║
║  │ Chapter      │ sect1.      │ <sect1>      │ ch (ch0001, ch0001s0001)│  ║
║  │ Preface      │ preface.    │ <preface>    │ pr (pr01, pref01)       │  ║
║  │ Appendix     │ appendix.   │ <appendix>   │ ap (ap01, app01)        │  ║
║  │ Dedication   │ dedication. │ <dedication> │ dd/de (dd01, ded01)     │  ║
║  │ Book Meta    │ book.       │ <book>       │ N/A                     │  ║
║  │ TOC          │ toc.        │ <toc>        │ N/A                     │  ║
║  └──────────────┴─────────────┴──────────────┴─────────────────────────┘  ║
║                                                                           ║
║  THE THREE GOLDEN RULES:                                                  ║
║  1. Filename prefix MUST match XML root element                           ║
║  2. Section ID prefix MUST match content type                             ║
║  3. All three (filename, root, section ID) MUST be consistent             ║
║                                                                           ║
║  ISBN FORMAT: 13 digits, no hyphens/spaces (e.g., 9781683674832)          ║
║                                                                           ║
╚═══════════════════════════════════════════════════════════════════════════╝
```

---

## Appendix B: Validation Script Template

```python
#!/usr/bin/env python3
"""
R2Library XML Validation Script
Run before submitting content packages
"""

import os
import re
import xml.etree.ElementTree as ET
from pathlib import Path

def validate_book_package(directory, isbn):
    errors = []
    warnings = []
    
    # Validate ISBN format
    if not re.match(r'^\d{13}$', isbn):
        errors.append(f"CRITICAL: Invalid ISBN format: {isbn}")
    
    # Check required files
    book_xml = Path(directory) / f"book.{isbn}.xml"
    toc_xml = Path(directory) / f"toc.{isbn}.xml"
    
    if not book_xml.exists():
        errors.append(f"CRITICAL: Missing required file: book.{isbn}.xml")
    
    if not toc_xml.exists():
        errors.append(f"CRITICAL: Missing required file: toc.{isbn}.xml")
    
    # Find all XML files
    xml_files = list(Path(directory).glob(f"*.{isbn}.*.xml"))
    
    # Validate each section file
    for xml_file in xml_files:
        filename = xml_file.name
        
        # Extract filename prefix
        prefix = filename.split('.')[0]
        
        # Skip book and toc files
        if prefix in ['book', 'toc']:
            continue
        
        # Parse XML
        try:
            tree = ET.parse(xml_file)
            root = tree.getroot()
            root_tag = root.tag
            root_id = root.get('id', '')
        except ET.ParseError as e:
            errors.append(f"XML PARSE ERROR in {filename}: {e}")
            continue
        
        # RULE 1: Filename prefix must match root element
        if prefix != root_tag:
            errors.append(
                f"RULE 1 VIOLATION in {filename}: "
                f"Filename prefix '{prefix}' does not match "
                f"root element '<{root_tag}>'"
            )
        
        # Extract section ID from filename
        parts = filename.replace('.xml', '').split('.')
        if len(parts) >= 3:
            section_id = parts[2]
            
            # RULE 2: Section ID prefix must match content type
            expected_content_type = get_content_type_from_prefix(prefix)
            actual_content_type = get_content_type_from_section_id(section_id)
            
            if expected_content_type != actual_content_type:
                errors.append(
                    f"RULE 2 VIOLATION in {filename}: "
                    f"Filename suggests {expected_content_type}, "
                    f"but section ID '{section_id}' suggests {actual_content_type}"
                )
        
        # Check for recommended elements
        title = root.find('.//title')
        if title is None or not title.text:
            warnings.append(f"Missing <title> in {filename}")
        
        risinfo = root.find('.//risinfo')
        if risinfo is None:
            warnings.append(f"Missing <risinfo> in {filename}")
        
        risindex = root.find('.//risindex')
        if risindex is None:
            warnings.append(f"Missing <risindex> in {filename}")
    
    return errors, warnings

def get_content_type_from_prefix(prefix):
    mapping = {
        'sect1': 'Chapter',
        'preface': 'Preface',
        'appendix': 'Appendix',
        'dedication': 'Dedication'
    }
    return mapping.get(prefix, 'Unknown')

def get_content_type_from_section_id(section_id):
    if len(section_id) < 2:
        return 'Unknown'
    
    prefix = section_id[:2].lower()
    mapping = {
        'ch': 'Chapter',
        'pr': 'Preface',
        'ap': 'Appendix',
        'dd': 'Dedication',
        'de': 'Dedication',
        'gl': 'Glossary',
        'bi': 'Bibliography'
    }
    return mapping.get(prefix, 'Chapter')

if __name__ == '__main__':
    import sys
    
    if len(sys.argv) < 3:
        print("Usage: python validate.py <directory> <isbn>")
        sys.exit(1)
    
    directory = sys.argv[1]
    isbn = sys.argv[2]
    
    errors, warnings = validate_book_package(directory, isbn)
    
    print(f"\n{'='*60}")
    print(f"VALIDATION REPORT FOR ISBN: {isbn}")
    print(f"{'='*60}\n")
    
    if errors:
        print(f"ERRORS ({len(errors)}):")
        for error in errors:
            print(f"  ❌ {error}")
        print()
    
    if warnings:
        print(f"WARNINGS ({len(warnings)}):")
        for warning in warnings:
            print(f"  ⚠️  {warning}")
        print()
    
    if not errors and not warnings:
        print("✅ All validations passed!")
    
    print(f"\nSummary: {len(errors)} errors, {len(warnings)} warnings")
    
    sys.exit(1 if errors else 0)
```

---

**Document End**
