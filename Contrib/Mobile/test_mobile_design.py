"""Dependency-free regression tests for native design-token export and structural checks."""
from __future__ import annotations

import json
import tempfile
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

from mobile_design import validate

OPEN = '<Styles xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><Styles.Resources>'
CLOSE = '</Styles.Resources></Styles>'


class MobileDesignTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.mobile = self.root / 'WalletWasabi.Fluent' / 'Mobile'
        self.styles = self.mobile / 'Styles'
        self.write_style('MobileTheme.axaml', '''<ResourceDictionary>
          <ResourceDictionary.ThemeDictionaries>
            <ResourceDictionary x:Key="Light"><SolidColorBrush x:Key="MobileText">#111111</SolidColorBrush></ResourceDictionary>
            <ResourceDictionary x:Key="Dark"><SolidColorBrush x:Key="MobileText">#EEEEEE</SolidColorBrush></ResourceDictionary>
          </ResourceDictionary.ThemeDictionaries>
          <x:Double x:Key="MobileTouchTarget">48</x:Double>
        </ResourceDictionary>''')

    def write_style(self, name: str, content: str) -> None:
        path = self.styles / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(OPEN + content + CLOSE, encoding='utf-8')

    def write_view(self, resource: str | None = None, code: bool = True) -> None:
        views = self.mobile / 'Views'
        views.mkdir(parents=True, exist_ok=True)
        child = '<Border/>' if resource is None else '<Border Background="{DynamicResource ' + resource + '}"/>'
        (views / 'Example.axaml').write_text(
            '<UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" x:Class="Example.ExampleView">'
            + child + '</UserControl>', encoding='utf-8')
        if code:
            (views / 'Example.cs').write_text('namespace Example; public sealed class ExampleView {}', encoding='utf-8')

    def test_exports_shared_component_and_composition_tokens(self) -> None:
        self.write_style('MobileControls.axaml', '<x:Double x:Key="MobilePageHeaderHeight">60</x:Double>')
        self.write_style('MobileCompositionTokens.axaml', '''<ResourceDictionary><ResourceDictionary.ThemeDictionaries>
          <ResourceDictionary x:Key="Light"><x:Int32 x:Key="MobileFeeColumns">1</x:Int32><x:Boolean x:Key="MobileLightLayout">True</x:Boolean></ResourceDictionary>
          <ResourceDictionary x:Key="Dark"><x:Int32 x:Key="MobileFeeColumns">3</x:Int32><x:Boolean x:Key="MobileLightLayout">False</x:Boolean></ResourceDictionary>
        </ResourceDictionary.ThemeDictionaries></ResourceDictionary>''')
        self.write_view('MobileText')
        result = validate(self.root)
        self.assertEqual(48, result['shared']['MobileTouchTarget']['value'])
        self.assertEqual(60, result['shared']['MobilePageHeaderHeight']['value'])
        self.assertEqual(1, result['themes']['Light']['MobileFeeColumns']['value'])
        self.assertEqual(3, result['themes']['Dark']['MobileFeeColumns']['value'])
        self.assertTrue(result['themes']['Light']['MobileLightLayout']['value'])
        self.assertFalse(result['themes']['Dark']['MobileLightLayout']['value'])
        self.assertEqual('WalletWasabi.Fluent/Mobile/Styles/MobileControls.axaml', result['tokenSources']['shared']['MobilePageHeaderHeight'])
        self.assertEqual(['Example.ExampleView'], result['structure']['viewClasses'])

    def test_includes_nested_style_folders(self) -> None:
        self.write_style('Components/Spacing.axaml', '<Thickness x:Key="MobileFooterPadding">16,8,16,16</Thickness>')
        result = validate(self.root)
        self.assertEqual('16,8,16,16', result['shared']['MobileFooterPadding']['value'])

    def test_helper_objects_are_not_exported_as_design_tokens(self) -> None:
        self.write_style('Helpers.axaml', '<ControlTheme x:Key="MobileClipboardButtonTheme" TargetType="Border"/>')
        self.assertNotIn('MobileClipboardButtonTheme', validate(self.root)['shared'])

    def test_export_is_deterministic_and_uses_relative_sources(self) -> None:
        first = json.dumps(validate(self.root), sort_keys=True, allow_nan=False)
        second = json.dumps(validate(self.root), sort_keys=True, allow_nan=False)
        self.assertEqual(first, second)
        self.assertNotIn(str(self.root), first)

    def test_duplicate_shared_tokens_are_rejected(self) -> None:
        self.write_style('Duplicate.axaml', '<x:Double x:Key="MobileTouchTarget">44</x:Double>')
        with self.assertRaisesRegex(ValueError, 'Duplicate shared token MobileTouchTarget'):
            validate(self.root)

    def test_duplicate_variant_tokens_are_rejected(self) -> None:
        self.write_style('Duplicate.axaml', '''<ResourceDictionary><ResourceDictionary.ThemeDictionaries>
          <ResourceDictionary x:Key="Light"><SolidColorBrush x:Key="MobileText">#222222</SolidColorBrush></ResourceDictionary>
        </ResourceDictionary.ThemeDictionaries></ResourceDictionary>''')
        with self.assertRaisesRegex(ValueError, 'Duplicate Light token MobileText'):
            validate(self.root)

    def test_theme_key_parity_includes_composition_files(self) -> None:
        self.write_style('Composition.axaml', '''<ResourceDictionary><ResourceDictionary.ThemeDictionaries>
          <ResourceDictionary x:Key="Dark"><x:Int32 x:Key="MobileFeeColumns">3</x:Int32></ResourceDictionary>
        </ResourceDictionary.ThemeDictionaries></ResourceDictionary>''')
        with self.assertRaisesRegex(ValueError, 'semantic token keys differ'):
            validate(self.root)

    def test_variant_type_mismatch_is_rejected(self) -> None:
        self.write_style('Composition.axaml', '''<ResourceDictionary><ResourceDictionary.ThemeDictionaries>
          <ResourceDictionary x:Key="Light"><x:Double x:Key="MobileSpacing">3</x:Double></ResourceDictionary>
          <ResourceDictionary x:Key="Dark"><x:String x:Key="MobileSpacing">three</x:String></ResourceDictionary>
        </ResourceDictionary.ThemeDictionaries></ResourceDictionary>''')
        with self.assertRaisesRegex(ValueError, 'token types differ'):
            validate(self.root)

    def test_nonfinite_numbers_are_rejected(self) -> None:
        for number in ['NaN', 'Infinity', '-Infinity']:
            with self.subTest(number=number):
                self.write_style('Invalid.axaml', f'<x:Double x:Key="MobileInvalid">{number}</x:Double>')
                with self.assertRaisesRegex(ValueError, 'Nonfinite'):
                    validate(self.root)

    def test_invalid_boolean_is_not_silently_false(self) -> None:
        self.write_style('Invalid.axaml', '<x:Boolean x:Key="MobileInvalid">maybe</x:Boolean>')
        with self.assertRaisesRegex(ValueError, 'Invalid Boolean'):
            validate(self.root)

    def test_int32_range_is_enforced(self) -> None:
        self.write_style('Invalid.axaml', '<x:Int32 x:Key="MobileInvalid">2147483648</x:Int32>')
        with self.assertRaisesRegex(ValueError, 'out of range'):
            validate(self.root)

    def test_invalid_gradient_colors_are_rejected(self) -> None:
        self.write_style('Invalid.axaml', '<LinearGradientBrush x:Key="MobileInvalid"><GradientStop Color="#GGGGGG" Offset="0"/></LinearGradientBrush>')
        with self.assertRaisesRegex(ValueError, 'Invalid color'):
            validate(self.root)

    def test_invalid_gradient_offsets_are_rejected(self) -> None:
        for offset in ['NaN', '2', '-1']:
            with self.subTest(offset=offset):
                self.write_style('Invalid.axaml', f'<LinearGradientBrush x:Key="MobileInvalid"><GradientStop Color="#111111" Offset="{offset}"/></LinearGradientBrush>')
                with self.assertRaisesRegex(ValueError, 'Invalid gradient stops'):
                    validate(self.root)

    def test_unsorted_gradient_stops_are_rejected(self) -> None:
        self.write_style('Invalid.axaml', '<LinearGradientBrush x:Key="MobileInvalid"><GradientStop Color="#111111" Offset="1"/><GradientStop Color="#222222" Offset="0"/></LinearGradientBrush>')
        with self.assertRaisesRegex(ValueError, 'Unsorted'):
            validate(self.root)

    def test_unknown_mobile_resource_is_rejected(self) -> None:
        self.write_view('MobileMissing')
        with self.assertRaisesRegex(ValueError, 'Undefined mobile resource references'):
            validate(self.root)

    def test_missing_view_class_is_rejected(self) -> None:
        self.write_view(code=False)
        with self.assertRaisesRegex(ValueError, 'No native C# view class'):
            validate(self.root)

    def test_invalid_xml_is_rejected(self) -> None:
        (self.styles / 'Invalid.axaml').write_text('<Styles>', encoding='utf-8')
        with self.assertRaises(ET.ParseError):
            validate(self.root)

    def test_incorrect_vector_arity_is_rejected(self) -> None:
        self.write_style('Invalid.axaml', '<Thickness x:Key="MobileInvalid">1,2,3</Thickness>')
        with self.assertRaisesRegex(ValueError, 'Invalid Thickness'):
            validate(self.root)


if __name__ == '__main__':
    unittest.main()
