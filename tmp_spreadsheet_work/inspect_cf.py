import zipfile
import xml.etree.ElementTree as ET
from pathlib import Path

path = Path('Assets/StellaStair/GameData/UnitAttackRanges.xlsx')
NS = {'main':'http://schemas.openxmlformats.org/spreadsheetml/2006/main','rel':'http://schemas.openxmlformats.org/officeDocument/2006/relationships'}
RNS = {'relpkg':'http://schemas.openxmlformats.org/package/2006/relationships'}

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

with zipfile.ZipFile(path, 'r') as z:
    paths = sheet_paths(z)
    for name in ['Target','Effect']:
        root = ET.fromstring(z.read(paths[name]))
        print('---', name, paths[name])
        for cf in root.findall('{%s}conditionalFormatting' % NS['main']):
            print(cf.attrib.get('sqref'))
            for rule in cf.findall('{%s}cfRule' % NS['main']):
                print(' ', rule.attrib)
                for f in rule.findall('{%s}formula' % NS['main']):
                    print('  formula', f.text)
