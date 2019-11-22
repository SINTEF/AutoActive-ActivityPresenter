# -*- coding: utf-8 -*-
"""
Spyder Editor

This is a temporary script file.
"""

import numpy as np
import pandas
import datetime

time = np.arange(1, 11, 0.5)
count = np.arange(1, 11)

pandas.DataFrame({'time': time, 'cos': np.cos(time), 'sin': np.sin(time), 'cos_t': time*np.cos(time),
                  'sin_t': time*np.sin(time)}).to_csv('sincos.csv', index=False)
pandas.DataFrame({'time': time, 'squared': time*time}).to_csv('squared.csv', index=False)

pandas.DataFrame({'time': time, 'count': range(len(time))}).to_csv('123.csv', index=False)

pandas.DataFrame({'time': time, 'count': range(len(time))}).to_csv('custom_header.csv', index=False)
with open('custom_header.csv', 'r+') as f:
    data = f.read()
    f.seek(0)
    f.write('this is a custom\nheader which\nshould not\nbe parsed\n')
    f.write(data)

pandas.DataFrame({'time': time, 'count': range(len(time))}).to_csv('dotsep.csv', index=False, sep='.')

pandas.DataFrame({'counter': count, 'double': count*2}).to_csv('notimename.csv', index=False)

pandas.DataFrame({'counter': count, 'double': count*2}).to_csv('withindex.csv')

pandas.DataFrame({'counter': count, 'double_and_half': count*2.5}).to_csv('excel_comma.csv', index=False, sep=';', decimal=',')

pandas.DataFrame({"epoch's": count, 'the double': count*2, 'speed [m/s]': count*np.pi}).to_csv('hardnames.csv', index=False)

pandas.DataFrame({'time': count, 'equal': count}).to_csv('changing_type.csv', index=False)

with open('changing_type.csv', 'r') as f:
    start = f.read(20)
    end = f.read()
with open('changing_type.csv', 'w') as f:
    f.write(start + '.14' + end)

pandas.DataFrame({'time': np.arange(1, 199), 'equal': np.arange(1, 199)}).to_csv('changing_type_late.csv', index=False)

with open('changing_type_late.csv', 'a+') as f:
    f.write('199,199.1\n')


start_time = datetime.datetime(2019, 11, 20, 13, 37, 00)
world_time = [ (start_time + datetime.timedelta(seconds=i)).isoformat() for i in range(30) ]

pandas.DataFrame({'timestamp': world_time, 'count': range(len(world_time)), 'count': range(len(world_time))*range(len(world_time))}).to_csv('changing_type.csv', index=False)