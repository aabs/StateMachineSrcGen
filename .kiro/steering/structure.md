# Project Structure

```
StateMachineSrcGen/
├── .kiro/
│   └── steering/                    # AI assistant steering rules
├── src/
│   ├── StateMachineSrcGen/          # Source generator project (netstandard2.0)
│   │   ├── Parsing/                 # Syntax tree analysis (pure functions)
│   │   ├── Analysis/                # Semantic model building (pure functions)
│   │   ├── Generation/              # C# code emission (pure functions)
│   │   ├── Diagnostics/             # Diagnostic descriptors and reporting
│   │   └── Pipeline/                # Incremental pipeline wiring
│   ├── StateMachineSrcGen.Attributes/ # Public marker attributes (lightweight, no Roslyn dependency)
│   └── StateMachineSrcGen.Tests/    # All tests (xUnit + FsCheck property-based)
│       ├── Parsing/                 # Property tests for syntax analysis
│       ├── Analysis/                # Property tests for model building
│       ├── Generation/              # Property tests + snapshot tests for emitted code
│       └── Integration/             # End-to-end in-memory compilation tests
└── StateMachineSrcGen.slnx          # Solution file (.slnx format)
```

## Conventions

- All projects live under `src/`
- Generator project targets `netstandard2.0`; test project targets `net9.0` (or latest)
- Attributes assembly is lightweight — no Roslyn dependency, so consumers only reference this
- Pipeline logic is structured as pure functions in Parsing → Analysis → Generation stages
- Each stage has a corresponding test folder with property-based tests
- Test project mirrors the generator project's folder structure for discoverability
