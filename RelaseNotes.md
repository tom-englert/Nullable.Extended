1.13
- Adjust colors to better match VS2022

1.12
- Update DGX to improve composite filter experience
- Improve reliability of the "Required" flag by analyzing each result individually.

1.11
- Remove the "Remove all not required tokens" command, it's of no practical use 

1.10
- improve data grid filtering experience
- Fix: detect justification comment on field declaration

1.9
- #10: Add a code fix provider

1.8
- #10: Fix justification, comments were not always detected properly

1.7
- #8: Improve keyboard navigation
- #9: Navigate to operator crashes VS2022
- #10: Recover the NullForgivingDetectionAnalyzer and support suppressions via `"// ! comment"`

1.6
- Fix #7: XAML assembly resolving fails for DGX

1.5
- Analysis results may depend on framework version: use the most relevant result.

1.4
- Support VS2022
- Fix #5: Add an icon to the tool window
- Fix multiple choice filter
- Fix detection of required null-forgiving operators

1.3
- Fix installation issues

1.2
- Use multiple choice filter for appropriate columns
- Show lean project name

1.1
- Fix #3: False negative when null forgiving operator is preceded by whitespace.
- Fix #2: Duplicate entries when project targets multiple frameworks
- Fix threading issues

1.0
- Initial release