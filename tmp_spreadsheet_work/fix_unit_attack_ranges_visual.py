import zipfile
import xml.etree.ElementTree as ET
from copy import deepcopy
from pathlib import Path

path = Path('Assets/StellaStair/GameData/UnitAttackRanges.xlsx')
NS = {'main': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main', 'rel': 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'}
RNS = {'relpkg': 'http://schemas.openxmlformats.org/package/2006/relationships'}
ET.register_namespace('', NS['main'])
ET.register_namespace('r', NS['rel'])

letters = ''
def col_name(n):
    s = ''
    while n:
        n, r = divmod(n - 1, 26)
        s = chr(65 + r) + s
    return s

def cell_ref(row, col):
    return f'{col_name(col)}{row}'

def set_cell_value(cell, value):
    for child in list(cell):
        if child.tag.endswith('}v') or child.tag.endswith('}is'):
            cell.remove(child)
    if value is None:
        cell.attrib.pop('t', None)
        return
    if isinstance(value, int):
        cell.attrib.pop('t', None)
        v = ET.SubElement(cell, f'{{{NS["main"]}}}v')
        v.text = str(value)
    else:
        cell.set('t', 'inlineStr')
        is_el = ET.SubElement(cell, f'{{{NS["main"]}}}is')
        t_el = ET.SubElement(is_el, f'{{{NS["main"]}}}t')
        t_el.text = value

def row_map(sheet_root):
    sheet_data = sheet_root.find('main:sheetData', NS)
    return {int(r.attrib['r']): r for r in sheet_data.findall('main:row', NS)}

def ensure_row(sheet_root, r_idx):
    sheet_data = sheet_root.find('main:sheetData', NS)
    rows = row_map(sheet_root)
    if r_idx in rows:
        return rows[r_idx]
    row = ET.Element(f'{{{NS["main"]}}}row', {'r': str(r_idx)})
    inserted = False
    for i, existing in enumerate(list(sheet_data)):
        if existing.tag.endswith('}row') and int(existing.attrib['r']) > r_idx:
            sheet_data.insert(i, row)
            inserted = True
            break
    if not inserted:
        sheet_data.append(row)
    return row

def cells_by_col(row):
    result = {}
    for c in row.findall('main:c', NS):
        ref = c.attrib.get('r', '')
        col = ''.join(ch for ch in ref if ch.isalpha())
        result[col] = c
    return result

def replace_row_from_template(sheet_root, src_row_idx, dst_row_idx, values):
    rows = row_map(sheet_root)
    src = rows[src_row_idx]
    dst_old = rows.get(dst_row_idx)
    dst = deepcopy(src)
    dst.attrib['r'] = str(dst_row_idx)
    for c in dst.findall('main:c', NS):
        old_ref = c.attrib.get('r', '')
        col = ''.join(ch for ch in old_ref if ch.isalpha())
        c.attrib['r'] = f'{col}{dst_row_idx}'
        col_idx = sum((ord(ch) - 64) * (26 ** i) for i, ch in enumerate(reversed(col)))
        set_cell_value(c, values[col_idx - 1] if col_idx - 1 < len(values) else None)
    sheet_data = sheet_root.find('main:sheetData', NS)
    if dst_old is not None:
        children = list(sheet_data)
        sheet_data.remove(dst_old)
        insert_at = 0
        for i, child in enumerate(children):
            if child is dst_old:
                insert_at = i
                break
        sheet_data.insert(insert_at, dst)
    else:
        inserted = False
        for i, existing in enumerate(list(sheet_data)):
            if existing.tag.endswith('}row') and int(existing.attrib['r']) > dst_row_idx:
                sheet_data.insert(i, dst)
                inserted = True
                break
        if not inserted:
            sheet_data.append(dst)

def set_dimension(sheet_root, ref):
    dim = sheet_root.find('main:dimension', NS)
    if dim is None:
        dim = ET.Element(f'{{{NS["main"]}}}dimension')
        sheet_root.insert(0, dim)
    dim.set('ref', ref)

def grid_cells(kind):
    cells = {}
    if kind == 'thrust':
        cells[(8,7)] = 0; cells[(8,8)] = 1; cells[(8,9)] = 0
    elif kind == 'piercing':
        cells[(8,8)] = 1
        for d in range(1, 6):
            cells[(8,8-d)] = 0; cells[(8,8+d)] = 0; cells[(8-d,8)] = 0; cells[(8+d,8)] = 0
    elif kind == 'bow':
        cells[(8,8)] = 1; cells[(7,8)] = 0; cells[(8,7)] = 0; cells[(8,9)] = 0; cells[(9,8)] = 0
    elif kind == 'harpoon':
        cells[(8,8)] = 1
        for d in range(1, 4):
            cells[(8,8-d)] = 0; cells[(8,8+d)] = 0
    return cells

def block_values(title, kind):
    rows = []
    row = [None] * 17
    row[0] = title
    rows.append(row)
    cells = grid_cells(kind)
    for rr in range(1, 16):
        row = [None] * 17
        for cc in range(1, 18):
            if (rr, cc) in cells:
                row[cc - 1] = cells[(rr, cc)]
        rows.append(row)
    return rows

def empty_effect_values(title):
    rows = [[title] + [None] * 16]
    rows += [[None] * 17 for _ in range(15)]
    return rows

def sheet_paths(z):
    workbook = ET.fromstring(z.read('xl/workbook.xml'))
    rels = ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))
    rel_targets = {r.attrib['Id']: r.attrib['Target'] for r in rels.findall('relpkg:Relationship', RNS)}
    result = {}
    for sheet in workbook.find('main:sheets', NS).findall('main:sheet', NS):
        name = sheet.attrib['name']
        rid = sheet.attrib[f'{{{NS["rel"]}}}id']
        target = rel_targets[rid]
        if target.startswith('/'):
            target = target[1:]
        elif not target.startswith('xl/'):
            target = 'xl/' + target
        result[name] = target
    return result

with zipfile.ZipFile(path, 'r') as zin:
    files = {name: zin.read(name) for name in zin.namelist()}
    paths = sheet_paths(zin)

target_root = ET.fromstring(files[paths['Target']])
effect_root = ET.fromstring(files[paths['Effect']])
assign_root = ET.fromstring(files[paths['Assignments']])

# Target: copy exact style/layout from existing Target 4 block rows 57:72 to new blocks.
for start, title, kind in [(75, 'Target 5', 'thrust'), (93, 'Target 6', 'piercing'), (111, 'Target 7', 'bow'), (129, 'Target 8', 'harpoon')]:
    vals = block_values(title, kind)
    for offset in range(16):
        replace_row_from_template(target_root, 57 + offset, start + offset, vals[offset])
set_dimension(target_root, 'A1:Q144')

# Effect: copy exact style/layout from existing Effect 2 block rows 21:36 to Effect 6.
vals = empty_effect_values('Effect 6')
for offset in range(16):
    replace_row_from_template(effect_root, 21 + offset, 39 + offset, vals[offset])
set_dimension(effect_root, 'A1:Q54')

# Assignments: copy A1:C6 style pattern to A1:D10 and set values.
assign_rows = [
    ['Unit','AttackMode','TargetRangeId','EffectRangeId'],
    ['Knight','Default','1','1'],
    ['Knight','Thrust','5','1'],
    ['Archer','Default','2','1'],
    ['Archer','PiercingArrow','6','6'],
    ['Archer','BowStrike','7','1'],
    ['Archer','Harpoon','8','1'],
    ['Wizard','Default','3','2'],
    ['EnemyGuard','Default','1','1'],
    ['EnemySoldier','Default','4','1'],
]
# use header row 1 and data row 2 templates; extend to D from C style by cloning cells.
for dst_idx, values in enumerate(assign_rows, start=1):
    src_idx = 1 if dst_idx == 1 else 2
    replace_row_from_template(assign_root, src_idx, dst_idx, values + [None] * (17 - len(values)))
    row = row_map(assign_root)[dst_idx]
    existing = cells_by_col(row)
    # Ensure D exists by cloning C if template only had A:C.
    if 'D' not in existing:
        c_src = existing.get('C')
        if c_src is not None:
            d = deepcopy(c_src)
            d.attrib['r'] = f'D{dst_idx}'
            set_cell_value(d, values[3])
            row.append(d)
set_dimension(assign_root, 'A1:D10')

files[paths['Target']] = ET.tostring(target_root, encoding='utf-8', xml_declaration=True)
files[paths['Effect']] = ET.tostring(effect_root, encoding='utf-8', xml_declaration=True)
files[paths['Assignments']] = ET.tostring(assign_root, encoding='utf-8', xml_declaration=True)

tmp = path.with_suffix('.xlsx.tmp')
with zipfile.ZipFile(tmp, 'w', zipfile.ZIP_DEFLATED) as zout:
    for name, data in files.items():
        zout.writestr(name, data)
tmp.replace(path)
print('openxml styles copied from existing blocks')
