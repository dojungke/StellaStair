import zipfile
import xml.etree.ElementTree as ET
from copy import deepcopy
from pathlib import Path

path = Path('Assets/StellaStair/GameData/UnitAttackRanges.xlsx')
NS = {'main':'http://schemas.openxmlformats.org/spreadsheetml/2006/main','rel':'http://schemas.openxmlformats.org/officeDocument/2006/relationships'}
RNS = {'relpkg':'http://schemas.openxmlformats.org/package/2006/relationships'}
ET.register_namespace('', NS['main'])
ET.register_namespace('r', NS['rel'])

def sheet_paths(z):
    workbook = ET.fromstring(z.read('xl/workbook.xml'))
    rels = ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))
    rel_targets = {r.attrib['Id']: r.attrib['Target'] for r in rels.findall('{%s}Relationship' % RNS['relpkg'])}
    result = {}
    for sheet in workbook.find('{%s}sheets' % NS['main']).findall('{%s}sheet' % NS['main']):
        rid = sheet.attrib['{%s}id' % NS['rel']]
        target = rel_targets[rid]
        if target.startswith('/'):
            target = target[1:]
        elif not target.startswith('xl/'):
            target = 'xl/' + target
        result[sheet.attrib['name']] = target
    return result

def remove_cf_for_sqrefs(root, sqrefs):
    for cf in list(root.findall('{%s}conditionalFormatting' % NS['main'])):
        if cf.attrib.get('sqref') in sqrefs:
            root.remove(cf)

def clone_rules(root, source_sqrefs, new_sqrefs, priority_start):
    sources = [cf for cf in root.findall('{%s}conditionalFormatting' % NS['main']) if cf.attrib.get('sqref') in source_sqrefs]
    # Keep order: source has 0 rule then 1 rule.
    priority = priority_start
    insert_after = None
    children = list(root)
    for i, child in enumerate(children):
        if child.tag == '{%s}conditionalFormatting' % NS['main']:
            insert_after = i
    insert_index = len(children) if insert_after is None else insert_after + 1
    for sqref in new_sqrefs:
        for src in sources:
            cf = deepcopy(src)
            cf.attrib['sqref'] = sqref
            for rule in cf.findall('{%s}cfRule' % NS['main']):
                rule.attrib['priority'] = str(priority)
                priority += 1
            root.insert(insert_index, cf)
            insert_index += 1
    return priority

with zipfile.ZipFile(path, 'r') as zin:
    files = {name: zin.read(name) for name in zin.namelist()}
    paths = sheet_paths(zin)

target_root = ET.fromstring(files[paths['Target']])
effect_root = ET.fromstring(files[paths['Effect']])
remove_cf_for_sqrefs(target_root, {'A76:O90','A94:O108','A112:O126','A130:O144'})
remove_cf_for_sqrefs(effect_root, {'A40:O54'})
clone_rules(target_root, ['A58:O72'], ['A76:O90','A94:O108','A112:O126','A130:O144'], 9)
clone_rules(effect_root, ['A22:O36'], ['A40:O54'], 5)
files[paths['Target']] = ET.tostring(target_root, encoding='utf-8', xml_declaration=True)
files[paths['Effect']] = ET.tostring(effect_root, encoding='utf-8', xml_declaration=True)

tmp = path.with_suffix('.xlsx.tmp')
with zipfile.ZipFile(tmp, 'w', zipfile.ZIP_DEFLATED) as zout:
    for name, data in files.items():
        zout.writestr(name, data)
tmp.replace(path)
print('conditional formatting extended')
